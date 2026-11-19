using FruitCatalog.API.Dtos;
using FruitCatalog.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FruitCatalog.API.Controllers;

[ApiController]
[Route("api/v1/catalog/items")]
public sealed class CatalogItemsController(ICatalogService catalogService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedCatalogResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedCatalogResponse>> GetItems(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await catalogService.GetItemsAsync(category, search, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType<FruitProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FruitProductDto>> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var item = await catalogService.GetItemByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Product Not Found",
                Detail = $"Fruit product with ID {id} was not found.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType<FruitProductDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FruitProductDto>> CreateItem(
        [FromBody] CreateFruitProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var created = await catalogService.CreateItemAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/reserve")]
    [AllowAnonymous]
    [ProducesResponseType<StockAdjustmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockAdjustmentResponse>> ReserveStock(
        int id,
        [FromBody] StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await catalogService.ReserveStockAsync(id, request.Quantity, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:int}/release")]
    [AllowAnonymous]
    [ProducesResponseType<StockAdjustmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockAdjustmentResponse>> ReleaseStock(
        int id,
        [FromBody] StockAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await catalogService.ReleaseStockAsync(id, request.Quantity, cancellationToken);
        return Ok(response);
    }
}
