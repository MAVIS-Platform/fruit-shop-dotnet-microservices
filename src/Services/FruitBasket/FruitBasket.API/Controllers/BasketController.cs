using System.Security.Claims;
using FruitBasket.API.Models;
using FruitBasket.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FruitBasket.API.Controllers;

[ApiController]
[Route("api/v1/basket")]
[Authorize]
public sealed class BasketController(IBasketService basketService) : ControllerBase
{
    [HttpGet("{customerId}")]
    [ProducesResponseType<CustomerBasket>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CustomerBasket>> GetBasket(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedForCustomer(customerId))
            return ForbiddenProblem();

        var basket = await basketService.GetBasketAsync(customerId, cancellationToken);
        return Ok(basket);
    }

    [HttpPut("{customerId}")]
    [ProducesResponseType<CustomerBasket>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CustomerBasket>> UpdateBasket(
        string customerId,
        [FromBody] UpdateBasketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedForCustomer(customerId))
            return ForbiddenProblem();

        var updated = await basketService.UpdateBasketAsync(customerId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{customerId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteBasket(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorizedForCustomer(customerId))
            return ForbiddenProblem();

        await basketService.DeleteBasketAsync(customerId, cancellationToken);
        return NoContent();
    }

    private bool IsAuthorizedForCustomer(string customerId)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Staff"))
            return true;

        var currentUserId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.Equals(currentUserId, customerId, StringComparison.OrdinalIgnoreCase);
    }

    private ObjectResult ForbiddenProblem() =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "You are not permitted to access or modify another customer's basket.",
            Instance = HttpContext.Request.Path
        });
}
