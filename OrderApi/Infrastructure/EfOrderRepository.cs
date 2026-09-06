namespace OrderApi.Infrastructure;

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderApi.Domain.Entities;
using OrderApi.Domain.Ports;

/// <summary>
/// Driven (Outbound) Adapter implementing the domain's IOrderRepository port.
/// Encapsulates EF Core queries, change tracking, and SQL commands.
/// Includes diagnostic profiling via Stopwatch for database read latency baseline.
/// </summary>
public class EfOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly ILogger<EfOrderRepository> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public EfOrderRepository(
        OrderDbContext context,
        ILogger<EfOrderRepository> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        await _context.Orders.AddAsync(order, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddWithOutboxAsync(Order order, OutboxMessage outboxMessage, CancellationToken ct = default)
    {
        await _context.Orders.AddAsync(order, ct);
        await _context.OutboxMessages.AddAsync(outboxMessage, ct);
        // Atomic commit: EF Core executes all changes inside a single database transaction.
        await _context.SaveChangesAsync(ct);
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // 1. Diagnostic Timing Baseline: Instrument DB read latency
        var stopwatch = Stopwatch.StartNew();

        // Notice: The caller never needs to know about .Include(o => o.Lines)
        // Aggregate consistency is maintained inside the adapter.
        var order = await _context.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        // 2. Structured Log Output
        _logger.LogInformation(
            "[PERF-BASELINE] EfOrderRepository.GetByIdAsync({OrderId}) completed in {ElapsedMs:F2} ms ({ElapsedTicks} ticks) [Found: {Found}]",
            id, elapsedMs, stopwatch.ElapsedTicks, order != null);

        // 3. Custom HTTP Diagnostic Header
        try
        {
            var response = _httpContextAccessor?.HttpContext?.Response;
            if (response != null && !response.HasStarted)
            {
                response.Headers["X-Query-Duration-Ms"] = elapsedMs.ToString("F2");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not set X-Query-Duration-Ms header");
        }

        return order;
    }
}
