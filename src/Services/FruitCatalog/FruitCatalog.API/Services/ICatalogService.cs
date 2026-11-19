using FruitCatalog.API.Dtos;

namespace FruitCatalog.API.Services;

public interface ICatalogService
{
    Task<PagedCatalogResponse> GetItemsAsync(
        string? category,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<FruitProductDto?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<FruitProductDto> CreateItemAsync(
        CreateFruitProductRequest request,
        CancellationToken cancellationToken = default);

    Task<StockAdjustmentResponse> ReserveStockAsync(
        int id,
        int quantity,
        CancellationToken cancellationToken = default);

    Task<StockAdjustmentResponse> ReleaseStockAsync(
        int id,
        int quantity,
        CancellationToken cancellationToken = default);
}
