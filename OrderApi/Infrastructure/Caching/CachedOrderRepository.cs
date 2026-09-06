namespace OrderApi.Infrastructure.Caching;

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrderApi.Domain.Entities;
using OrderApi.Domain.Ports;

/// <summary>
/// Decorator Pattern: Implements IOrderRepository by decorating the primary persistence adapter
/// with the Cache-Aside pattern using Redis (IDistributedCache).
/// Leaves the Domain Core and EfOrderRepository completely untouched.
/// </summary>
public class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedOrderRepository> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public CachedOrderRepository(
        IOrderRepository inner,
        IDistributedCache cache,
        ILogger<CachedOrderRepository> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var cacheKey = $"order:{id}";
        var stopwatch = Stopwatch.StartNew();

        // -------------------------------------------------------------------------
        // 1. Check Redis Cache
        // -------------------------------------------------------------------------
        try
        {
            var cachedJson = await _cache.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrEmpty(cachedJson))
            {
                stopwatch.Stop();
                var hitDurationMs = stopwatch.Elapsed.TotalMilliseconds;

                var cacheDto = JsonSerializer.Deserialize<OrderCacheDto>(cachedJson);
                if (cacheDto != null)
                {
                    _logger.LogInformation(
                        "[PERF-CACHE] Cache HIT for {CacheKey} in {Duration:F2} ms ({Ticks} ticks)",
                        cacheKey, hitDurationMs, stopwatch.ElapsedTicks);

                    SetDiagnosticHeaders("HIT", hitDurationMs);
                    return ReconstituteOrder(cacheDto);
                }
            }
        }
        catch (Exception ex)
        {
            // Cache resilience: Redis unavailability must NOT break core database queries
            _logger.LogWarning(ex, "[PERF-CACHE] Redis query failed for {CacheKey}. Falling back to DB.", cacheKey);
            SetDiagnosticHeaders("ERROR-FALLBACK", null);
        }

        // -------------------------------------------------------------------------
        // 2. Cache Miss: Query Inner Persistence Adapter (EF Core)
        // -------------------------------------------------------------------------
        _logger.LogInformation("[PERF-CACHE] Cache MISS for {CacheKey}. Querying database...", cacheKey);
        SetDiagnosticHeaders("MISS", null);

        var order = await _inner.GetByIdAsync(id, ct);
        if (order == null)
        {
            return null;
        }

        // -------------------------------------------------------------------------
        // 3. Write-Back to Redis with 5-Minute Absolute Expiration
        // -------------------------------------------------------------------------
        try
        {
            var cacheDto = MapToCacheDto(order);
            var serialized = JsonSerializer.Serialize(cacheDto);
            await _cache.SetStringAsync(cacheKey, serialized, CacheOptions, ct);

            _logger.LogInformation(
                "[PERF-CACHE] Successfully cached {CacheKey} with 5-minute absolute TTL.",
                cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PERF-CACHE] Failed to write {CacheKey} to Redis.", cacheKey);
        }

        return order;
    }

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        // 1. Mutate state via inner persistence adapter
        await _inner.AddAsync(order, ct);

        // 2. Cache-Aside Invalidation: Evict any stale cache entry for this aggregate
        var cacheKey = $"order:{order.Id}";
        try
        {
            await _cache.RemoveAsync(cacheKey, ct);
            _logger.LogInformation("[PERF-CACHE] Cache invalidated for {CacheKey} after mutation.", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PERF-CACHE] Failed to invalidate {CacheKey} from Redis.", cacheKey);
        }
    }

    public async Task AddWithOutboxAsync(Order order, OutboxMessage outboxMessage, CancellationToken ct = default)
    {
        // 1. Mutate state and stage outbox message atomically via inner persistence adapter
        await _inner.AddWithOutboxAsync(order, outboxMessage, ct);

        // 2. Cache-Aside Invalidation: Evict any stale cache entry for this aggregate
        var cacheKey = $"order:{order.Id}";
        try
        {
            await _cache.RemoveAsync(cacheKey, ct);
            _logger.LogInformation("[PERF-CACHE] Cache invalidated for {CacheKey} after mutation.", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PERF-CACHE] Failed to invalidate {CacheKey} from Redis.", cacheKey);
        }
    }

    private void SetDiagnosticHeaders(string cacheStatus, double? durationMs)
    {
        try
        {
            var response = _httpContextAccessor?.HttpContext?.Response;
            if (response != null && !response.HasStarted)
            {
                response.Headers["X-Cache"] = cacheStatus;
                if (durationMs.HasValue)
                {
                    response.Headers["X-Query-Duration-Ms"] = durationMs.Value.ToString("F2");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not set diagnostic headers");
        }
    }

    #region Reconstitution & DTO Mapping (Zero Domain Pollution)

    private static OrderCacheDto MapToCacheDto(Order order)
    {
        return new OrderCacheDto(
            order.Id,
            order.CustomerName,
            order.CreatedAtUtc,
            order.TotalAmount,
            order.Lines.Select(l => new OrderLineCacheDto(l.ProductName, l.Quantity, l.UnitPrice)).ToList()
        );
    }

    private static Order ReconstituteOrder(OrderCacheDto dto)
    {
        var orderLines = dto.Lines
            .Select(l => new OrderLine(l.ProductName, l.Quantity, l.UnitPrice))
            .ToList();

        // Invoke private constructor: Order(Guid, string, IEnumerable<OrderLine>, DateTime)
        var ctor = typeof(Order).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(Guid), typeof(string), typeof(IEnumerable<OrderLine>), typeof(DateTime) },
            null);

        if (ctor != null)
        {
            return (Order)ctor.Invoke(new object[] { dto.Id, dto.CustomerName, orderLines, dto.CreatedAtUtc });
        }

        // Alternative reflection fallback
        var order = (Order)Activator.CreateInstance(typeof(Order), nonPublic: true)!;
        typeof(Order).GetProperty(nameof(Order.Id))?.SetValue(order, dto.Id);
        typeof(Order).GetProperty(nameof(Order.CustomerName))?.SetValue(order, dto.CustomerName);
        typeof(Order).GetProperty(nameof(Order.CreatedAtUtc))?.SetValue(order, dto.CreatedAtUtc);
        typeof(Order).GetProperty(nameof(Order.TotalAmount))?.SetValue(order, dto.TotalAmount);

        var linesField = typeof(Order).GetField("_lines", BindingFlags.NonPublic | BindingFlags.Instance);
        if (linesField != null && linesField.GetValue(order) is List<OrderLine> list)
        {
            list.AddRange(orderLines);
        }

        return order;
    }

    private sealed record OrderCacheDto(
        Guid Id,
        string CustomerName,
        DateTime CreatedAtUtc,
        decimal TotalAmount,
        List<OrderLineCacheDto> Lines);

    private sealed record OrderLineCacheDto(
        string ProductName,
        int Quantity,
        decimal UnitPrice);

    #endregion
}
