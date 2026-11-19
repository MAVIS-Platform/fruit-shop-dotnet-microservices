using EventBus.Contracts;
using FruitOrdering.API.Clients;
using FruitOrdering.API.Data;
using FruitOrdering.API.Domain;
using FruitOrdering.API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FruitOrdering.API.Services;

/// <summary>
/// Orchestrates order creation via a 3-step Saga (Semantic Lock -> Reserve Stock -> Confirm + Outbox)
/// with reverse stock compensation on partial failure.
/// </summary>
public sealed class OrderSagaService(
    OrderingDbContext dbContext,
    ICatalogApiClient catalogApiClient,
    IBasketApiClient basketApiClient,
    ILogger<OrderSagaService> logger) : IOrderSagaService
{
    public async Task<OrderResponse> PlaceOrderAsync(
        string customerId,
        string idempotencyKey,
        string correlationId,
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency-Key header is required.");

        var normalizedKey = idempotencyKey.Trim();
        var existingOrder = await FindExistingOrderByIdempotencyKeyAsync(normalizedKey, cancellationToken);
        if (existingOrder is not null)
        {
            if (!string.Equals(existingOrder.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenAccessException("Idempotency-Key belongs to a different customer.");

            logger.LogInformation("Idempotent replay for key {IdempotencyKey} -> Order {OrderId}.", normalizedKey, existingOrder.Id);
            return existingOrder;
        }

        var mergedItems = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new OrderItemRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        var address = new Address(
            request.ShippingAddress.Street.Trim(),
            request.ShippingAddress.Ward.Trim(),
            request.ShippingAddress.City.Trim());

        var order = Order.CreateDraft(customerId, normalizedKey, address);

        foreach (var item in mergedItems)
        {
            var catalogItem = await catalogApiClient.GetItemAsync(item.ProductId, cancellationToken)
                ?? throw new DomainException($"Fruit product {item.ProductId} does not exist in catalog.");

            order.AddItem(catalogItem.Id, catalogItem.Name, catalogItem.UnitPrice, item.Quantity);
        }

        // T1: Semantic Lock (Submitted)
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        var reservedItems = new List<(int ProductId, int Quantity)>();

        try
        {
            // T2: Reserve stock across catalog items
            foreach (var orderItem in order.Items)
            {
                await catalogApiClient.ReserveStockAsync(orderItem.ProductId, orderItem.Quantity, cancellationToken);
                reservedItems.Add((orderItem.ProductId, orderItem.Quantity));
            }

            // T3: Pivot transaction + Transactional Outbox in one atomic SaveChangesAsync
            order.MarkAsConfirmed();
            var eventId = Guid.NewGuid();
            var createdEvent = new OrderCreatedIntegrationEvent(
                eventId,
                DateTime.UtcNow,
                correlationId,
                order.Id,
                order.CustomerId,
                order.TotalAmount);

            dbContext.IntegrationEventLogEntries.Add(IntegrationEventLogEntry.Create(eventId, createdEvent));
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Order {OrderId} confirmed for customer {CustomerId} (Total: {TotalAmount}).",
                order.Id, order.CustomerId, order.TotalAmount);

            return MapToResponse(order);
        }
        catch (Exception ex) when (ex is OrderConflictException or ServiceUnavailableException or DomainException or OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Saga failed for Order {OrderId}; executing reverse compensation for {ReservedCount} reserved item(s).",
                order.Id, reservedItems.Count);

            await CompensateReservedStockAsync(reservedItems);

            var reason = string.IsNullOrWhiteSpace(ex.Message) ? "Order saga compensated due to downstream failure." : ex.Message;
            order.Cancel(reason);

            var cancelEventId = Guid.NewGuid();
            var cancelledEvent = new OrderCancelledIntegrationEvent(
                cancelEventId,
                DateTime.UtcNow,
                correlationId,
                order.Id,
                order.CustomerId,
                reason);

            dbContext.IntegrationEventLogEntries.Add(IntegrationEventLogEntry.Create(cancelEventId, cancelledEvent));
            await dbContext.SaveChangesAsync(CancellationToken.None);

            throw;
        }
    }

    public async Task<OrderResponse> CheckoutFromBasketAsync(
        string customerId,
        string idempotencyKey,
        string correlationId,
        string? bearerToken,
        CheckoutBasketRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Idempotency-Key header is required.");

        var normalizedKey = idempotencyKey.Trim();
        var existingOrder = await FindExistingOrderByIdempotencyKeyAsync(normalizedKey, cancellationToken);
        if (existingOrder is not null)
        {
            if (!string.Equals(existingOrder.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenAccessException("Idempotency-Key belongs to a different customer.");

            return existingOrder;
        }

        var basket = await basketApiClient.GetBasketAsync(customerId, bearerToken, cancellationToken);
        if (basket.Items.Count == 0)
            throw new DomainException("Cannot checkout an empty basket.");

        var createRequest = new CreateOrderRequest(
            request.ShippingAddress,
            basket.Items.Select(i => new OrderItemRequest(i.ProductId, i.Quantity)).ToList());

        return await PlaceOrderAsync(customerId, normalizedKey, correlationId, createRequest, cancellationToken);
    }

    private async Task CompensateReservedStockAsync(List<(int ProductId, int Quantity)> reservedItems)
    {
        for (var i = reservedItems.Count - 1; i >= 0; i--)
        {
            var (productId, quantity) = reservedItems[i];
            try
            {
                await catalogApiClient.ReleaseStockAsync(productId, quantity, CancellationToken.None);
            }
            catch (Exception releaseEx)
            {
                logger.LogError(releaseEx,
                    "Compensation release failed for Product {ProductId} (Quantity {Quantity}). Manual reconciliation required.",
                    productId, quantity);
            }
        }
    }

    private async Task<OrderResponse?> FindExistingOrderByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(x => x.IdempotencyKey == idempotencyKey)
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
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static OrderResponse MapToResponse(Order order) =>
        new(
            order.Id,
            order.CustomerId,
            order.IdempotencyKey,
            order.Status.ToString(),
            order.FailureReason,
            order.TotalAmount,
            new AddressDto(order.ShippingAddress.Street, order.ShippingAddress.Ward, order.ShippingAddress.City),
            order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.ProductNameSnapshot,
                i.UnitPriceSnapshot,
                i.Quantity,
                i.LineTotal)).ToList(),
            order.CreatedAtUtc);
}
