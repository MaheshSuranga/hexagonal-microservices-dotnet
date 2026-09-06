namespace OrderApi.Infrastructure;

using Microsoft.EntityFrameworkCore;
using OrderApi.Domain.Entities;
using OrderApi.Domain.Ports;

/// <summary>
/// Driven (Outbound) Adapter implementing the domain's IOrderRepository port.
/// Encapsulates EF Core queries, change tracking, and SQL commands.
/// </summary>
public class EfOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;

    public EfOrderRepository(OrderDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        await _context.Orders.AddAsync(order, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // Notice: The caller never needs to know about .Include(o => o.Lines)
        // Aggregate consistency is maintained inside the adapter.
        return await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }
}
