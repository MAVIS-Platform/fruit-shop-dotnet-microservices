using EventBus.Contracts;
using FruitBasket.API.Consumers;
using FruitBasket.API.Repositories;
using FruitBasket.API.Services;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FruitShop.UnitTests;

public sealed class BasketConsumerIdempotencyTests
{
    [Fact]
    public async Task Consume_FirstTimeEvent_DeletesBasketAndMarksProcessed()
    {
        var basketService = Substitute.For<IBasketService>();
        var processedStore = Substitute.For<IProcessedEventStore>();
        var context = Substitute.For<ConsumeContext<OrderCreatedIntegrationEvent>>();

        var eventId = Guid.NewGuid();
        var evt = new OrderCreatedIntegrationEvent(
            eventId,
            DateTime.UtcNow,
            "corr-01",
            Guid.NewGuid(),
            "customer-01",
            190000m);

        context.Message.Returns(evt);
        context.CancellationToken.Returns(CancellationToken.None);
        processedStore.HasProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(false);

        var consumer = new OrderCreatedConsumer(
            basketService,
            processedStore,
            NullLogger<OrderCreatedConsumer>.Instance);

        await consumer.Consume(context);

        await basketService.Received(1).DeleteBasketAsync("customer-01", Arg.Any<CancellationToken>());
        await processedStore.Received(1).MarkProcessedAsync(eventId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_DuplicateEvent_SkipsBasketDeletion()
    {
        var basketService = Substitute.For<IBasketService>();
        var processedStore = Substitute.For<IProcessedEventStore>();
        var context = Substitute.For<ConsumeContext<OrderCreatedIntegrationEvent>>();

        var eventId = Guid.NewGuid();
        var evt = new OrderCreatedIntegrationEvent(
            eventId,
            DateTime.UtcNow,
            "corr-02",
            Guid.NewGuid(),
            "customer-01",
            190000m);

        context.Message.Returns(evt);
        context.CancellationToken.Returns(CancellationToken.None);
        processedStore.HasProcessedAsync(eventId, Arg.Any<CancellationToken>()).Returns(true);

        var consumer = new OrderCreatedConsumer(
            basketService,
            processedStore,
            NullLogger<OrderCreatedConsumer>.Instance);

        await consumer.Consume(context);

        await basketService.DidNotReceive().DeleteBasketAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await processedStore.DidNotReceive().MarkProcessedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
