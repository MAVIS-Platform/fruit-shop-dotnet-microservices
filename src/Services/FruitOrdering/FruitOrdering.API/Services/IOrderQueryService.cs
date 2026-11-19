using FruitOrdering.API.Dtos;

namespace FruitOrdering.API.Services;

public interface IOrderQueryService
{
    Task<OrderResponse> GetOrderByIdAsync(
        Guid orderId,
        string requesterId,
        bool isPrivileged,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderSummaryDto>> GetOrdersByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default);
}
