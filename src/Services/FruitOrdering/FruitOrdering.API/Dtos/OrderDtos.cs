using System.ComponentModel.DataAnnotations;

namespace FruitOrdering.API.Dtos;

public sealed record AddressDto(
    [Required, MaxLength(200)] string Street,
    [Required, MaxLength(100)] string Ward,
    [Required, MaxLength(100)] string City);

public sealed record OrderItemRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Range(1, 100_000)] int Quantity);

public sealed record CreateOrderRequest(
    [Required] AddressDto ShippingAddress,
    [Required, MinLength(1)] List<OrderItemRequest> Items);

public sealed record CheckoutBasketRequest(
    [Required] AddressDto ShippingAddress);

public sealed record OrderItemDto(
    int ProductId,
    string ProductNameSnapshot,
    decimal UnitPriceSnapshot,
    int Quantity,
    decimal LineTotal);

public sealed record OrderResponse(
    Guid Id,
    string CustomerId,
    string IdempotencyKey,
    string Status,
    string? FailureReason,
    decimal TotalAmount,
    AddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    DateTime CreatedAtUtc);

public sealed record OrderSummaryDto(
    Guid Id,
    string CustomerId,
    string Status,
    decimal TotalAmount,
    int ItemCount,
    DateTime CreatedAtUtc);
