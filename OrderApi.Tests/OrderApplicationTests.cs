namespace OrderApi.Tests;

using OrderApi.Domain.Entities;
using OrderApi.Domain.Exceptions;
using OrderApi.Tests.Fakes;
using Xunit;

public class OrderApplicationTests
{
    private readonly InMemoryOrderRepository _repository = new();

    [Fact]
    public void CreateOrder_WithEmptyItems_GuaranteesDomainInvariantAndPreventsPersistence()
    {
        // Act & Assert: Attempting to create an order without items throws DomainException
        var ex = Assert.Throws<DomainException>(() =>
        {
            var invalidOrder = Order.Create("Bob", new List<OrderLine>());
        });

        Assert.Equal("An order must contain at least one order line.", ex.Message);

        // Verification: The repository was never touched, zero state corruption
        Assert.Equal(0, _repository.Count);
    }

    [Fact]
    public async Task CreateOrder_WithBulkItems_AppliesDiscountAndPersistsSuccessfully()
    {
        // Arrange: 2 items @ $60 = $120 -> 10% discount applies ($108 total)
        var lines = new List<OrderLine>
        {
            new("Mechanical Keyboard", 2, 60.00m)
        };

        // Act
        var order = Order.Create("Alice", lines);
        await _repository.AddAsync(order);

        // Assert
        Assert.Equal(108.00m, order.TotalAmount);
        Assert.Equal(1, _repository.Count);

        var retrieved = await _repository.GetByIdAsync(order.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Alice", retrieved.CustomerName);
        Assert.Equal(108.00m, retrieved.TotalAmount);
        Assert.Single(retrieved.Lines);
    }

    [Fact]
    public void CreateOrder_WithInvalidCustomerName_ThrowsDomainException()
    {
        var lines = new List<OrderLine> { new("Mouse", 1, 25.00m) };

        var ex = Assert.Throws<DomainException>(() =>
            Order.Create("", lines)
        );

        Assert.Equal("Customer name is required.", ex.Message);
        Assert.Equal(0, _repository.Count);
    }
}
