using FruitCatalog.API.Data;
using FruitCatalog.API.Domain;
using FruitCatalog.API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FruitCatalog.API.Services;

public sealed class CatalogService(
    CatalogDbContext dbContext,
    ILogger<CatalogService> logger) : ICatalogService
{
    public async Task<PagedCatalogResponse> GetItemsAsync(
        string? category,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedPageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = dbContext.FruitProducts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            query = query.Where(x => x.Category.ToLower() == cat);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.ToLower().Contains(term) ||
                x.Origin.ToLower().Contains(term) ||
                x.Sku.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new FruitProductDto(
                x.Id,
                x.Sku,
                x.Name,
                x.Category,
                x.Origin,
                x.Unit,
                x.UnitPrice,
                x.AvailableStock,
                x.Version,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedCatalogResponse(items, totalCount, normalizedPage, normalizedPageSize);
    }

    public async Task<FruitProductDto?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.FruitProducts
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new FruitProductDto(
                x.Id,
                x.Sku,
                x.Name,
                x.Category,
                x.Origin,
                x.Unit,
                x.UnitPrice,
                x.AvailableStock,
                x.Version,
                x.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<FruitProductDto> CreateItemAsync(
        CreateFruitProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedSku = request.Sku.Trim().ToUpperInvariant();
        var exists = await dbContext.FruitProducts
            .AnyAsync(x => x.Sku == normalizedSku, cancellationToken);

        if (exists)
            throw new DuplicateSkuException(normalizedSku);

        var product = FruitProduct.Create(
            normalizedSku,
            request.Name,
            request.Category,
            request.Origin,
            request.Unit,
            request.UnitPrice,
            request.InitialStock);

        dbContext.FruitProducts.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created fruit product {ProductId} ({Sku})", product.Id, product.Sku);

        return new FruitProductDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Category,
            product.Origin,
            product.Unit,
            product.UnitPrice,
            product.AvailableStock,
            product.Version,
            product.CreatedAtUtc);
    }

    public async Task<StockAdjustmentResponse> ReserveStockAsync(
        int id,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.FruitProducts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        product.ReserveStock(quantity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Reserved {Quantity} {Unit} of product {ProductId} ({Sku}). Remaining stock: {Stock}",
            quantity, product.Unit, product.Id, product.Sku, product.AvailableStock);

        return new StockAdjustmentResponse(product.Id, product.Sku, product.AvailableStock, product.Version);
    }

    public async Task<StockAdjustmentResponse> ReleaseStockAsync(
        int id,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var product = await dbContext.FruitProducts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ProductNotFoundException(id);

        product.ReleaseStock(quantity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Released {Quantity} {Unit} back to product {ProductId} ({Sku}). Available stock: {Stock}",
            quantity, product.Unit, product.Id, product.Sku, product.AvailableStock);

        return new StockAdjustmentResponse(product.Id, product.Sku, product.AvailableStock, product.Version);
    }
}
