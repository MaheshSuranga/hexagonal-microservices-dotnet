namespace OrderApi.Services;

using Microsoft.EntityFrameworkCore;
using OrderApi.Data;

public class OrderService
{
    private readonly AppDbContext _context;

    public OrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        // Business rule 1: Set audit timestamp
        order.CreatedAt = DateTime.UtcNow;

        // Business rule 2: Calculate total amount based on items
        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        // Business rule 3: Apply 10% discount if total exceeds $100
        if (order.TotalAmount > 100m)
        {
            order.TotalAmount *= 0.90m;
        }

        // Direct coupling to EF Core Change Tracker and persistence
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return order;
    }

    public async Task<Order?> GetOrderByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<Order>> GetAllOrdersAsync()
    {
        return await _context.Orders
            .Include(o => o.Items)
            .ToListAsync();
    }
}
