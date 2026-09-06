using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                      ?? "Host=localhost;Database=inventory_db;Username=inventory_user;Password=inventory_pass";

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

// Ensure DB schema and seed initial inventory stock
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    db.Database.EnsureCreated();

    if (!db.InventoryItems.Any())
    {
        db.InventoryItems.AddRange(
            new InventoryItem { Sku = "KEYBOARD-001", QuantityAvailable = 50 },
            new InventoryItem { Sku = "MOUSE-001", QuantityAvailable = 100 },
            new InventoryItem { Sku = "MONITOR-001", QuantityAvailable = 20 }
        );
        db.SaveChanges();
    }
}

// -----------------------------------------------------------------------------
// Inventory Endpoints
// -----------------------------------------------------------------------------

// GET /api/inventory/{sku}
app.MapGet("/api/inventory/{sku}", async (string sku, InventoryDbContext db) =>
{
    var item = await db.InventoryItems
        .AsNoTracking()
        .FirstOrDefaultAsync(i => i.Sku.ToLower() == sku.ToLower());

    return item is not null
        ? Results.Ok(new { sku = item.Sku, quantityAvailable = item.QuantityAvailable })
        : Results.NotFound(new { error = "Not Found", message = $"Item with SKU '{sku}' does not exist." });
});

// POST /api/inventory/reserve
app.MapPost("/api/inventory/reserve", async (ReserveStockRequest request, InventoryDbContext db) =>
{
    if (request.Quantity <= 0)
    {
        return Results.BadRequest(new { error = "Invalid Quantity", message = "Reserved quantity must be greater than zero." });
    }

    var item = await db.InventoryItems
        .FirstOrDefaultAsync(i => i.Sku.ToLower() == request.Sku.ToLower());

    if (item is null)
    {
        return Results.NotFound(new { error = "Not Found", message = $"Item with SKU '{request.Sku}' does not exist." });
    }

    if (item.QuantityAvailable < request.Quantity)
    {
        return Results.BadRequest(new
        {
            error = "Insufficient Stock",
            message = $"Only {item.QuantityAvailable} units available for SKU '{request.Sku}'. Requested: {request.Quantity}."
        });
    }

    // Deduct stock (atomic within transaction)
    item.QuantityAvailable -= request.Quantity;
    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        success = true,
        sku = item.Sku,
        reserved = request.Quantity,
        remainingStock = item.QuantityAvailable
    });
});

app.Run();

// -----------------------------------------------------------------------------
// Models & Data Context
// -----------------------------------------------------------------------------
public record ReserveStockRequest(string Sku, int Quantity);

public class InventoryItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int QuantityAvailable { get; set; }
}

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<InventoryItem>(b =>
        {
            b.ToTable("InventoryItems");
            b.HasKey(i => i.Id);
            b.HasIndex(i => i.Sku).IsUnique();
            b.Property(i => i.Sku).IsRequired().HasMaxLength(100);
            b.Property(i => i.QuantityAvailable).IsRequired();
        });
    }
}
