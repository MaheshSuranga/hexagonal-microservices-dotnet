using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using OrderApi.Domain.Ports;
using OrderApi.Infrastructure;
using OrderApi.Infrastructure.Messaging;
using OrderApi.Workers;

var builder = WebApplication.CreateBuilder(args);

// Driving Adapter (HTTP Presentation)
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// -----------------------------------------------------------------------------
// Persistence Driven Adapter (EF Core)
// -----------------------------------------------------------------------------
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                         ?? "Data Source=orders_hex.db";

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    if (dbConnectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        dbConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(dbConnectionString);
    }
    else
    {
        options.UseSqlite(dbConnectionString);
    }
});

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();

// -----------------------------------------------------------------------------
// Messaging Driven Adapter & Background Workers (Azure Service Bus)
// -----------------------------------------------------------------------------
var sbConnectionString = builder.Configuration.GetConnectionString("ServiceBus");

if (!string.IsNullOrWhiteSpace(sbConnectionString))
{
    // 1. Singleton ServiceBusClient lifecycle (Crucial for AMQP socket reuse)
    builder.Services.AddSingleton(new ServiceBusClient(sbConnectionString));

    // 2. Register Driven Port (IEventPublisher) -> ServiceBusEventPublisher
    builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

    // 3. Register Dual BackgroundService Consumers
    builder.Services.AddHostedService<EmailWorker>();
    builder.Services.AddHostedService<AnalyticsWorker>();
}
else
{
    // Fallback for running locally without active Service Bus connection
    builder.Services.AddSingleton<IEventPublisher, LoggingEventPublisher>();
}

var app = builder.Build();

// Ensure database and schema are created at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
