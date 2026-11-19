using FruitOrdering.API.Data;
using FruitOrdering.API.Domain;
using FruitOrdering.API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FruitOrdering.API.Services;

public sealed class OrderQueryService(OrderingDbContext dbContext) : IOrderQueryService
{
    public async Task<OrderResponse> GetOrderByIdAsync(
        Guid orderId,
        string requesterId,
        bool isPrivileged,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => new OrderResponse(
                x.Id,
                x.CustomerId,
                x.IdempotencyKey,
                x.Status.ToString(),
                x.FailureReason,
                x.TotalAmount,
                new AddressDto(x.ShippingAddress.Street, x.ShippingAddress.Ward, x.ShippingAddress.City),
                x.Items.Select(i => new OrderItemDto(
                    i.ProductId,
                    i.ProductNameSnapshot,
                    i.UnitPriceSnapshot,
                    i.Quantity,
                    i.UnitPriceSnapshot * i.Quantity)).ToList(),
                x.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        if (!isPrivileged && !string.Equals(order.CustomerId, requesterId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException("You are not authorized to view another customer's order.");
        }

        return order;
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersByCustomerAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new OrderSummaryDto(
                x.Id,
                x.CustomerId,
                x.Status.ToString(),
                x.TotalAmount,
                x.Items.Count,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
