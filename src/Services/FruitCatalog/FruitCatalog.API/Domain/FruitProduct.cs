using System.ComponentModel.DataAnnotations;

namespace FruitCatalog.API.Domain;

/// <summary>
/// Rich domain entity representing a Vietnamese specialty fruit product in catalog_db.
/// Uses integer Version concurrency token for full PostgreSQL and EF InMemory compatibility.
/// </summary>
public sealed class FruitProduct
{
    private FruitProduct()
    {
    }

    public int Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Origin { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int AvailableStock { get; private set; }

    [ConcurrencyCheck]
    public int Version { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static FruitProduct Create(
        string sku,
        string name,
        string category,
        string origin,
        string unit,
        decimal unitPrice,
        int initialStock)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("SKU cannot be empty.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name cannot be empty.");
        if (string.IsNullOrWhiteSpace(category))
            throw new DomainException("Category cannot be empty.");
        if (string.IsNullOrWhiteSpace(origin))
            throw new DomainException("Origin cannot be empty.");
        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("Unit cannot be empty.");
        if (unitPrice <= 0)
            throw new DomainException("Unit price must be greater than zero.");
        if (initialStock < 0)
            throw new DomainException("Initial stock cannot be negative.");

        return new FruitProduct
        {
            Sku = sku.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Category = category.Trim(),
            Origin = origin.Trim(),
            Unit = unit.Trim(),
            UnitPrice = unitPrice,
            AvailableStock = initialStock,
            Version = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Reservation quantity must be greater than zero.");

        if (AvailableStock < quantity)
            throw new InsufficientStockException(Id, quantity, AvailableStock);

        AvailableStock -= quantity;
        Version++;
    }

    public void ReleaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Release quantity must be greater than zero.");

        AvailableStock += quantity;
        Version++;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new DomainException("Updated unit price must be greater than zero.");

        UnitPrice = newPrice;
        Version++;
    }
}
