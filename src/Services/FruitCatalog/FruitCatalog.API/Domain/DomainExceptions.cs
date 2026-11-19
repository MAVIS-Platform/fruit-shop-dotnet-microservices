namespace FruitCatalog.API.Domain;

public class DomainException(string message) : Exception(message);

public sealed class InsufficientStockException(int productId, int requestedQuantity, int availableStock)
    : DomainException($"Insufficient stock for product {productId}. Requested: {requestedQuantity}, Available: {availableStock}.")
{
    public int ProductId { get; } = productId;
    public int RequestedQuantity { get; } = requestedQuantity;
    public int AvailableStock { get; } = availableStock;
}

public sealed class ProductNotFoundException(int productId)
    : DomainException($"Fruit product with ID {productId} was not found.")
{
    public int ProductId { get; } = productId;
}

public sealed class DuplicateSkuException(string sku)
    : DomainException($"A fruit product with SKU '{sku}' already exists.")
{
    public string Sku { get; } = sku;
}
