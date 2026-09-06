namespace OrderApi.Infrastructure;

using Microsoft.EntityFrameworkCore;
using OrderApi.Domain.Entities;

/// <summary>
/// EF Core DbContext acting as part of the Driven Adapter.
/// Maps the pure Domain Aggregate Root (Order) and value objects/entities (OrderLine)
/// to SQLite relational tables without polluting the domain model with EF attributes.
/// </summary>
public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(builder =>
        {
            builder.ToTable("Orders");
            builder.HasKey(o => o.Id);

            builder.Property(o => o.CustomerName)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(o => o.CreatedAtUtc)
                   .IsRequired();

            builder.Property(o => o.TotalAmount)
                   .HasPrecision(18, 2)
                   .IsRequired();

            // Map encapsulated OrderLine collection as owned entities (DDD Aggregate pattern)
            builder.OwnsMany(o => o.Lines, line =>
            {
                line.ToTable("OrderLines");
                line.WithOwner().HasForeignKey("OrderId");
                line.Property<int>("Id");
                line.HasKey("Id");

                line.Property(l => l.ProductName)
                    .IsRequired()
                    .HasMaxLength(200);

                line.Property(l => l.Quantity)
                    .IsRequired();

                line.Property(l => l.UnitPrice)
                    .HasPrecision(18, 2)
                    .IsRequired();
            });

            // Use backing field access to preserve private collection encapsulation
            builder.Navigation(o => o.Lines)
                   .UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
