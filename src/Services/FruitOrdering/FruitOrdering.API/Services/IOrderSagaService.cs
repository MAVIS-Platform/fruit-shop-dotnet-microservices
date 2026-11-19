using FruitOrdering.API.Dtos;

namespace FruitOrdering.API.Services;

public interface IOrderSagaService
{
    Task<OrderResponse> PlaceOrderAsync(
        string customerId,
        string idempotencyKey,
        string correlationId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderResponse> CheckoutFromBasketAsync(
        string customerId,
        string idempotencyKey,
        string correlationId,
        string? bearerToken,
        CheckoutBasketRequest request,
        CancellationToken cancellationToken = default);
}
