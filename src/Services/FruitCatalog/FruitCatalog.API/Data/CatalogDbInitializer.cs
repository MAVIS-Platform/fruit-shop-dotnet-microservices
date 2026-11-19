using FruitCatalog.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace FruitCatalog.API.Data;

public static class CatalogDbInitializer
{
    public static async Task InitializeAsync(
        CatalogDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Safe when multiple scaled replicas race during cold-start schema creation.
            logger.LogWarning(ex, "Concurrent EnsureCreatedAsync detected; continuing startup.");
        }

        if (await dbContext.FruitProducts.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Catalog database already seeded.");
            return;
        }

        var seedProducts = new[]
        {
            FruitProduct.Create("FR-XOAI-01", "Xoài Cát Hòa Lộc Loại 1", "Xoài", "Cái Bè - Tiền Giang", "kg", 95000m, 80),
            FruitProduct.Create("FR-SAURIENG-01", "Sầu Riêng Ri6 Cơm Vàng", "Sầu Riêng", "Chợ Lách - Bến Tre", "kg", 165000m, 45),
            FruitProduct.Create("FR-BUOI-01", "Bưởi Da Xanh Bến Tre", "Bưởi", "Bến Tre", "trái", 65000m, 100),
            FruitProduct.Create("FR-THANHLONG-01", "Thanh Long Ruột Đỏ Bình Thuận", "Thanh Long", "Bình Thuận", "kg", 42000m, 120),
            FruitProduct.Create("FR-CAM-01", "Cam Sành Vĩnh Long", "Cam", "Vĩnh Long", "kg", 35000m, 150),
            FruitProduct.Create("FR-MANCAU-01", "Mãng Cầu Xiêm Tươi", "Mãng Cầu", "Tiền Giang", "kg", 58000m, 60)
        };

        try
        {
            await dbContext.FruitProducts.AddRangeAsync(seedProducts, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} Vietnamese specialty fruits into catalog_db.", seedProducts.Length);
        }
        catch (DbUpdateException ex)
        {
            logger.LogInformation(ex, "Catalog seed skipped because another replica seeded simultaneously.");
        }
    }
}
