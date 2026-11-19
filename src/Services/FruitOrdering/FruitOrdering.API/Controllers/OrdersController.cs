using System.Security.Claims;
using FruitOrdering.API.Domain;
using FruitOrdering.API.Dtos;
using FruitOrdering.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FruitOrdering.API.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public sealed class OrdersController(
    IOrderSagaService orderSagaService,
    IOrderQueryService orderQueryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderResponse>> PlaceOrder(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency-Key header is required.");

        var customerId = GetCurrentUserId();
        var correlationId = GetCorrelationId();

        var order = await orderSagaService.PlaceOrderAsync(
            customerId,
            idempotencyKey,
            correlationId,
            request,
            cancellationToken);

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpPost("checkout-basket")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderResponse>> CheckoutBasket(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CheckoutBasketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency-Key header is required.");

        var customerId = GetCurrentUserId();
        var correlationId = GetCorrelationId();
        var bearerToken = ExtractBearerToken();

        var order = await orderSagaService.CheckoutFromBasketAsync(
            customerId,
            idempotencyKey,
            correlationId,
            bearerToken,
            request,
            cancellationToken);

        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrderById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var requesterId = GetCurrentUserId();
        var isPrivileged = User.IsInRole("Admin") || User.IsInRole("Staff");

        var order = await orderQueryService.GetOrderByIdAsync(id, requesterId, isPrivileged, cancellationToken);
        return Ok(order);
    }

    [HttpGet("my")]
    [ProducesResponseType<IReadOnlyList<OrderSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetMyOrders(
        CancellationToken cancellationToken = default)
    {
        var customerId = GetCurrentUserId();
        var orders = await orderQueryService.GetOrdersByCustomerAsync(customerId, cancellationToken);
        return Ok(orders);
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId)
            ? throw new UnauthorizedAccessException("Authenticated user subject claim is missing.")
            : userId;
    }

    private string GetCorrelationId()
    {
        var headerValue = Request.Headers["X-Correlation-Id"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(headerValue) ? HttpContext.TraceIdentifier : headerValue;
    }

    private string? ExtractBearerToken()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authHeader["Bearer ".Length..].Trim();
    }
}
