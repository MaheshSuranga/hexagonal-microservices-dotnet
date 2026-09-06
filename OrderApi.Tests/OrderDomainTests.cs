namespace OrderApi.Tests;

using OrderApi.Domain.Entities;
using OrderApi.Domain.Exceptions;
using Xunit;

public class OrderDomainTests
{
    [Fact]
    public void Create_WithEmptyLines_ThrowsDomainException()
    {
        // Act & Assert: Pure domain unit test - 0 mocks, 0 DB engines, executes in microseconds!
        var ex = Assert.Throws<DomainException>(() =>
            Order.Create("Alice", new List<OrderLine>())
        );

        Assert.Equal("An order must contain at least one order line.", ex.Message);
    }

    [Fact]
    public void Create_WithBlankCustomerName_ThrowsDomainException()
    {
        var validLine = new OrderLine("Keyboard", 1, 50m);

        var ex = Assert.Throws<DomainException>(() =>
            Order.Create("   ", new List<OrderLine> { validLine })
        );

        Assert.Equal("Customer name is required.", ex.Message);
    }

    [Fact]
    public void Create_WithTotalExceeding100_Applies10PercentDiscountAutomatically()
    {
        // 2 items * $60 = $120 -> 10% discount -> $108
        var line = new OrderLine("Keyboard", 2, 60m);

        var order = Order.Create("Alice", new List<OrderLine> { line });

        Assert.Equal(108m, order.TotalAmount);
        Assert.Single(order.Lines);
        Assert.NotEqual(Guid.Empty, order.Id);
    }
}
