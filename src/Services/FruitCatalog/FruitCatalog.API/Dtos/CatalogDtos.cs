using System.ComponentModel.DataAnnotations;

namespace FruitCatalog.API.Dtos;

public sealed record FruitProductDto(
    int Id,
    string Sku,
    string Name,
    string Category,
    string Origin,
    string Unit,
    decimal UnitPrice,
    int AvailableStock,
    int Version,
    DateTime CreatedAtUtc);

public sealed record PagedCatalogResponse(
    IReadOnlyList<FruitProductDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record CreateFruitProductRequest(
    [Required, MaxLength(50)] string Sku,
    [Required, MaxLength(200)] string Name,
    [Required, MaxLength(100)] string Category,
    [Required, MaxLength(150)] string Origin,
    [Required, MaxLength(30)] string Unit,
    [Range(0.01, 100_000_000)] decimal UnitPrice,
    [Range(0, 1_000_000)] int InitialStock);

public sealed record StockAdjustmentRequest(
    [Range(1, 100_000)] int Quantity);

public sealed record StockAdjustmentResponse(
    int ProductId,
    string Sku,
    int AvailableStock,
    int Version);
