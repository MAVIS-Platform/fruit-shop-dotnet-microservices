using System.ComponentModel.DataAnnotations;

namespace FruitBasket.API.Models;

public sealed record BasketItem(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string Unit);

public sealed record CustomerBasket(
    string CustomerId,
    List<BasketItem> Items);

public sealed record BasketItemRequest(
    [Range(1, int.MaxValue)] int ProductId,
    [Required, MaxLength(200)] string ProductName,
    [Range(0.01, 100_000_000)] decimal UnitPrice,
    [Range(1, 100_000)] int Quantity,
    [Required, MaxLength(30)] string Unit);

public sealed record UpdateBasketRequest(
    [Required] List<BasketItemRequest> Items);
