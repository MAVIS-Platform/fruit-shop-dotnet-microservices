namespace FruitOrdering.API.Domain;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; private set; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPriceSnapshot * Quantity;

    internal static OrderItem Create(
        Guid orderId,
        int productId,
        string productNameSnapshot,
        decimal unitPriceSnapshot,
        int quantity)
    {
        if (productId <= 0)
            throw new DomainException("ProductId must be positive.");
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new DomainException("Product name snapshot cannot be empty.");
        if (unitPriceSnapshot <= 0)
            throw new DomainException("Unit price snapshot must be greater than zero.");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductNameSnapshot = productNameSnapshot.Trim(),
            UnitPriceSnapshot = unitPriceSnapshot,
            Quantity = quantity
        };
    }
}
