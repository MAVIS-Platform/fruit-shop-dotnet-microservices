using FruitOrdering.API.Domain;
using Xunit;

namespace FruitShop.UnitTests;

public sealed class OrderDomainTests
{
    private static Address SampleAddress() => new("123 Lê Lợi", "Phường Bến Thành", "TP. Hồ Chí Minh");

    [Fact]
    public void CreateDraft_InitializesSubmittedOrder()
    {
        var order = Order.CreateDraft("customer-01", "idem-001", SampleAddress());

        Assert.Equal(OrderStatus.Submitted, order.Status);
        Assert.Equal(0m, order.TotalAmount);
        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_CalculatesTotalAmountAndStoresSnapshot()
    {
        var order = Order.CreateDraft("customer-01", "idem-002", SampleAddress());

        order.AddItem(1, "Xoài Cát Hòa Lộc", 95000m, 2);
        order.AddItem(2, "Sầu Riêng Ri6", 165000m, 1);

        Assert.Equal(2, order.Items.Count);
        Assert.Equal(355000m, order.TotalAmount);
    }

    [Fact]
    public void MarkAsConfirmed_OnEmptyOrder_ThrowsDomainException()
    {
        var order = Order.CreateDraft("customer-01", "idem-003", SampleAddress());

        var ex = Assert.Throws<DomainException>(() => order.MarkAsConfirmed());
        Assert.Contains("no items", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkAsConfirmed_WithItems_TransitionsToConfirmed()
    {
        var order = Order.CreateDraft("customer-01", "idem-004", SampleAddress());
        order.AddItem(1, "Bưởi Da Xanh", 65000m, 3);

        order.MarkAsConfirmed();

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.UpdatedAtUtc);
    }

    [Fact]
    public void Cancel_FromSubmitted_TransitionsToCancelledAndSetsReason()
    {
        var order = Order.CreateDraft("customer-01", "idem-005", SampleAddress());
        order.AddItem(1, "Cam Sành", 35000m, 4);

        order.Cancel("Insufficient stock for item 2");

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Insufficient stock for item 2", order.FailureReason);
    }

    [Fact]
    public void Cancel_FromConfirmed_ThrowsDomainException()
    {
        var order = Order.CreateDraft("customer-01", "idem-006", SampleAddress());
        order.AddItem(1, "Cam Sành", 35000m, 2);
        order.MarkAsConfirmed();

        Assert.Throws<DomainException>(() => order.Cancel("Late cancel"));
    }
}
