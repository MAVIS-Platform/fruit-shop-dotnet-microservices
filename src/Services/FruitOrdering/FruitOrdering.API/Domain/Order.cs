namespace FruitOrdering.API.Domain;

public sealed record Address(string Street, string Ward, string City);

public enum OrderStatus
{
    Submitted = 0,
    Confirmed = 1,
    Cancelled = 2
}

/// <summary>
/// DDD Aggregate Root representing a customer fruit order with semantic lock (Submitted) and compensation states.
/// </summary>
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public Address ShippingAddress { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Order CreateDraft(string customerId, string idempotencyKey, Address shippingAddress)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new DomainException("CustomerId is required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("IdempotencyKey is required.");
        if (shippingAddress is null ||
            string.IsNullOrWhiteSpace(shippingAddress.Street) ||
            string.IsNullOrWhiteSpace(shippingAddress.Ward) ||
            string.IsNullOrWhiteSpace(shippingAddress.City))
        {
            throw new DomainException("Complete shipping address (street, ward, city) is required.");
        }

        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            ShippingAddress = shippingAddress,
            Status = OrderStatus.Submitted,
            TotalAmount = 0m,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void AddItem(int productId, string productNameSnapshot, decimal unitPriceSnapshot, int quantity)
    {
        if (Status != OrderStatus.Submitted)
            throw new DomainException($"Cannot modify items when order is in '{Status}' status.");

        var item = OrderItem.Create(Id, productId, productNameSnapshot, unitPriceSnapshot, quantity);
        _items.Add(item);
        TotalAmount = _items.Sum(i => i.UnitPriceSnapshot * i.Quantity);
    }

    public void MarkAsConfirmed()
    {
        if (Status != OrderStatus.Submitted)
            throw new DomainException($"Cannot confirm order in '{Status}' status.");

        if (_items.Count == 0)
            throw new DomainException("Cannot confirm an order with no items.");

        Status = OrderStatus.Confirmed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            throw new DomainException("Order is already cancelled.");
        if (Status == OrderStatus.Confirmed)
            throw new DomainException("Confirmed orders cannot be cancelled directly via saga compensation.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Cancellation reason is required.");

        Status = OrderStatus.Cancelled;
        FailureReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
