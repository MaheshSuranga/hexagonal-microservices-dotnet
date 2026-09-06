namespace OrderApi.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using OrderApi.Data;
using OrderApi.Services;
using Xunit;

public class OrderServiceTests
{
    /// <summary>
    /// ATTEMPT 1: Mocking DbContext and DbSet with Moq.
    /// This test documents the instant crash developers face when trying to mock EF Core.
    /// </summary>
    [Fact]
    public void Attempt1_MockingDbContext_FailsImmediatelyWithNonVirtualMembers()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().Options;
        var mockDbSet = new Mock<DbSet<Order>>();
        var mockContext = new Mock<AppDbContext>(options);

        // This line crashes: In AppDbContext, "DbSet<Order> Orders" is non-virtual.
        // Moq throws: System.NotSupportedException: Non-overridable members may not be used in setup.
        var ex = Assert.Throws<NotSupportedException>(() =>
        {
            mockContext.Setup(c => c.Orders).Returns(mockDbSet.Object);
        });

        Assert.Contains("Non-overridable members", ex.Message);
    }

    /// <summary>
    /// ATTEMPT 2: Surrendering and spinning up an In-Memory SQLite connection just to test a unit of logic.
    /// This test exposes that the business rule doesn't even exist in the service!
    /// </summary>
    [Fact]
    public async Task Attempt2_SpinningUpInfrastructure_RevealsBusinessRuleIsMissingFromService()
    {
        // -------------------------------------------------------------------------
        // CEREMONY: Must spin up an in-memory SQLite database connection just to
        // execute a single business logic unit test!
        // -------------------------------------------------------------------------
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new AppDbContext(options))
        {
            context.Database.EnsureCreated();
            var service = new OrderService(context);

            // Business Case: Order with empty line items
            var invalidOrder = new Order
            {
                CustomerName = "Alice",
                Items = new List<OrderItem>() // Empty items!
            };

            // -------------------------------------------------------------------------
            // THE FATAL FLAW:
            // This assertion FAILS because the validation rule was placed in OrdersController.cs!
            // OrderService blindly accepts the order, calculates TotalAmount = 0,
            // inserts it into SQLite, and returns it successfully.
            // -------------------------------------------------------------------------
            var result = await service.CreateOrderAsync(invalidOrder);

            // The order was persisted despite violating core domain rules:
            Assert.NotNull(result);
            Assert.Equal(0, result.TotalAmount);
            Assert.True(result.Id > 0, "Corrupted order was successfully written to the database!");
        }
    }
}
