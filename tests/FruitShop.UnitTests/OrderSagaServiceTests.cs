using EventBus.Contracts;
using FruitOrdering.API.Clients;
using FruitOrdering.API.Data;
using FruitOrdering.API.Domain;
using FruitOrdering.API.Dtos;
using FruitOrdering.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FruitShop.UnitTests;

public sealed class OrderSagaServiceTests
{
    private static OrderingDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderingDbContext>()
            .UseInMemoryDatabase($"ordering-test-{Guid.NewGuid():N}")
            .Options;

        return new OrderingDbContext(options);
    }

    private static CreateOrderRequest SampleCreateOrderRequest() =>
        new(
            new AddressDto("45 Nguyễn Huệ", "Bến Nghé", "TP. Hồ Chí Minh"),
            [
                new OrderItemRequest(1, 2),
                new OrderItemRequest(2, 1)
            ]);

    [Fact]
    public async Task PlaceOrderAsync_HappyPath_ConfirmsOrderAndWritesOutboxEvent()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var catalogClient = Substitute.For<ICatalogApiClient>();
        var basketClient = Substitute.For<IBasketApiClient>();

        catalogClient.GetItemAsync(1, Arg.Any<CancellationToken>())
            .Returns(new CatalogFruitItemDto(1, "FR-XOAI-01", "Xoài Cát Hòa Lộc", "kg", 95000m, 80));
        catalogClient.GetItemAsync(2, Arg.Any<CancellationToken>())
            .Returns(new CatalogFruitItemDto(2, "FR-SAURIENG-01", "Sầu Riêng Ri6", "kg", 165000m, 45));

        var saga = new OrderSagaService(
            dbContext,
            catalogClient,
            basketClient,
            NullLogger<OrderSagaService>.Instance);

        var result = await saga.PlaceOrderAsync(
            "customer-01",
            "idem-happy-01",
            "corr-100",
            SampleCreateOrderRequest());

        Assert.Equal("Confirmed", result.Status);
        Assert.Equal(355000m, result.TotalAmount);
        Assert.Equal(2, result.Items.Count);

        await catalogClient.Received(1).ReserveStockAsync(1, 2, Arg.Any<CancellationToken>());
        await catalogClient.Received(1).ReserveStockAsync(2, 1, Arg.Any<CancellationToken>());
        await catalogClient.DidNotReceive().ReleaseStockAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        var outboxEntries = await dbContext.IntegrationEventLogEntries.ToListAsync();
        Assert.Single(outboxEntries);
        Assert.Equal(nameof(OrderCreatedIntegrationEvent), outboxEntries[0].EventType);
        Assert.Equal(OutboxEventState.NotPublished, outboxEntries[0].State);
    }

    [Fact]
    public async Task PlaceOrderAsync_WhenSecondItemOutOfStock_CompensatesFirstItemAndMarksCancelled()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var catalogClient = Substitute.For<ICatalogApiClient>();
        var basketClient = Substitute.For<IBasketApiClient>();

        catalogClient.GetItemAsync(1, Arg.Any<CancellationToken>())
            .Returns(new CatalogFruitItemDto(1, "FR-XOAI-01", "Xoài Cát Hòa Lộc", "kg", 95000m, 80));
        catalogClient.GetItemAsync(2, Arg.Any<CancellationToken>())
            .Returns(new CatalogFruitItemDto(2, "FR-SAURIENG-01", "Sầu Riêng Ri6", "kg", 165000m, 0));

        catalogClient.ReserveStockAsync(1, 2, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        catalogClient.ReserveStockAsync(2, 1, Arg.Any<CancellationToken>())
            .ThrowsAsync(new OrderConflictException("Insufficient stock for product 2."));

        var saga = new OrderSagaService(
            dbContext,
            catalogClient,
            basketClient,
            NullLogger<OrderSagaService>.Instance);

        await Assert.ThrowsAsync<OrderConflictException>(() =>
            saga.PlaceOrderAsync("customer-01", "idem-fail-02", "corr-200", SampleCreateOrderRequest()));

        // Must compensate item 1 in reverse without releasing unreserved item 2.
        await catalogClient.Received(1).ReleaseStockAsync(1, 2, Arg.Any<CancellationToken>());
        await catalogClient.DidNotReceive().ReleaseStockAsync(2, Arg.Any<int>(), Arg.Any<CancellationToken>());

        var savedOrder = await dbContext.Orders.SingleAsync();
        Assert.Equal(OrderStatus.Cancelled, savedOrder.Status);
        Assert.Contains("Insufficient stock", savedOrder.FailureReason);

        var outboxEntries = await dbContext.IntegrationEventLogEntries.ToListAsync();
        Assert.Single(outboxEntries);
        Assert.Equal(nameof(OrderCancelledIntegrationEvent), outboxEntries[0].EventType);
    }

    [Fact]
    public async Task PlaceOrderAsync_WithDuplicateIdempotencyKey_ReturnsExistingOrderWithoutReservingAgain()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var catalogClient = Substitute.For<ICatalogApiClient>();
        var basketClient = Substitute.For<IBasketApiClient>();

        catalogClient.GetItemAsync(1, Arg.Any<CancellationToken>())
            .Returns(new CatalogFruitItemDto(1, "FR-XOAI-01", "Xoài Cát Hòa Lộc", "kg", 95000m, 80));

        var saga = new OrderSagaService(
            dbContext,
            catalogClient,
            basketClient,
            NullLogger<OrderSagaService>.Instance);

        var request = new CreateOrderRequest(
            new AddressDto("45 Nguyễn Huệ", "Bến Nghé", "TP. Hồ Chí Minh"),
            [new OrderItemRequest(1, 3)]);

        var first = await saga.PlaceOrderAsync("customer-01", "idem-dup-03", "corr-300", request);
        var second = await saga.PlaceOrderAsync("customer-01", "idem-dup-03", "corr-301", request);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(285000m, second.TotalAmount);
        await catalogClient.Received(1).ReserveStockAsync(1, 3, Arg.Any<CancellationToken>());
    }
}
