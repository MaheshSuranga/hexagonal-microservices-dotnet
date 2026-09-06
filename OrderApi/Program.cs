using Microsoft.EntityFrameworkCore;
using OrderApi.Domain.Ports;
using OrderApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Driving Adapter (HTTP Presentation)
builder.Services.AddControllers();

// Driven Adapter Configuration (Hexagonal Port Registration)
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=orders_hex.db"));

// Dependency Inversion: Register Driven Port (IOrderRepository) to Driven Adapter (EfOrderRepository)
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();

var app = builder.Build();

// Ensure SQLite database and tables are created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.EnsureCreated();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
