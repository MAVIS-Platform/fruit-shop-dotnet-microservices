namespace FruitOrdering.API.Domain;

public class DomainException(string message) : Exception(message);

public sealed class OrderConflictException(string message) : DomainException(message);

public sealed class OrderNotFoundException(Guid orderId)
    : DomainException($"Order with ID '{orderId}' was not found.")
{
    public Guid OrderId { get; } = orderId;
}

public sealed class ServiceUnavailableException(string serviceName, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string ServiceName { get; } = serviceName;
}

public sealed class ForbiddenAccessException(string message) : Exception(message);
