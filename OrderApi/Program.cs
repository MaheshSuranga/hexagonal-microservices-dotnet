using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using OrderApi.Domain.Ports;
using OrderApi.Infrastructure;
using OrderApi.Infrastructure.Caching;
using OrderApi.Infrastructure.Messaging;
using OrderApi.Workers;

var builder = WebApplication.CreateBuilder(args);

// Driving Adapter (HTTP Presentation)
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// -----------------------------------------------------------------------------
// Distributed Caching (Redis)
// -----------------------------------------------------------------------------
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "OrderService_";
});

// -----------------------------------------------------------------------------
// Persistence Driven Adapter (EF Core) & Cache-Aside Decorator
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

// 1. Register core EF Core repository as concrete type
builder.Services.AddScoped<EfOrderRepository>();

// 2. Decorate IOrderRepository with CachedOrderRepository via native Microsoft DI
builder.Services.AddScoped<IOrderRepository>(sp =>
{
    var inner = sp.GetRequiredService<EfOrderRepository>();
    var cache = sp.GetRequiredService<IDistributedCache>();
    var logger = sp.GetRequiredService<ILogger<CachedOrderRepository>>();
    var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
    return new CachedOrderRepository(inner, cache, logger, httpContextAccessor);
});

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
