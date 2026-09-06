using Microsoft.EntityFrameworkCore;
using OrderApi.Domain.Ports;
using OrderApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Driving Adapter (HTTP Presentation)
builder.Services.AddControllers();

// Driven Adapter Configuration (Hexagonal Port Registration)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                      ?? "Data Source=orders_hex.db";

builder.Services.AddDbContext<OrderDbContext>(options =>
{
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// Dependency Inversion: Register Driven Port (IOrderRepository) to Driven Adapter (EfOrderRepository)
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();

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
