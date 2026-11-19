using FruitCatalog.API.Domain;
using Xunit;

namespace FruitShop.UnitTests;

public sealed class CatalogDomainTests
{
    [Fact]
    public void ReserveStock_WhenSufficient_DecrementsStockAndIncrementsVersion()
    {
        var product = FruitProduct.Create(
            "FR-XOAI-01",
            "Xoài Cát Hòa Lộc",
            "Xoài",
            "Tiền Giang",
            "kg",
            95000m,
            50);

        product.ReserveStock(15);

        Assert.Equal(35, product.AvailableStock);
        Assert.Equal(2, product.Version);
    }

    [Fact]
    public void ReserveStock_WhenInsufficient_ThrowsInsufficientStockException()
    {
        var product = FruitProduct.Create(
            "FR-SAURIENG-01",
            "Sầu Riêng Ri6",
            "Sầu Riêng",
            "Bến Tre",
            "kg",
            165000m,
            5);

        var ex = Assert.Throws<InsufficientStockException>(() => product.ReserveStock(10));

        Assert.Equal(10, ex.RequestedQuantity);
        Assert.Equal(5, ex.AvailableStock);
        Assert.Equal(5, product.AvailableStock);
        Assert.Equal(1, product.Version);
    }

    [Fact]
    public void ReleaseStock_AddsQuantityAndIncrementsVersion()
    {
        var product = FruitProduct.Create(
            "FR-BUOI-01",
            "Bưởi Da Xanh",
            "Bưởi",
            "Bến Tre",
            "trái",
            65000m,
            20);

        product.ReserveStock(5);
        product.ReleaseStock(5);

        Assert.Equal(20, product.AvailableStock);
        Assert.Equal(3, product.Version);
    }

    [Fact]
    public void UpdatePrice_WithPositiveAmount_UpdatesPriceAndIncrementsVersion()
    {
        var product = FruitProduct.Create(
            "FR-CAM-01",
            "Cam Sành Vĩnh Long",
            "Cam",
            "Vĩnh Long",
            "kg",
            35000m,
            100);

        product.UpdatePrice(38000m);

        Assert.Equal(38000m, product.UnitPrice);
        Assert.Equal(2, product.Version);
    }
}
