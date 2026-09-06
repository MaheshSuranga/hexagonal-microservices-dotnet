namespace OrderApi.Domain.Entities;

using OrderApi.Domain.Exceptions;

/// <summary>
/// Aggregate Root representing an Order in the business domain.
/// Encapsulates state, guards invariants, and enforces business rules.
/// </summary>
public class Order
{
    private readonly List<OrderLine> _lines = new();

    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public decimal TotalAmount { get; private set; }

    /// <summary>
    /// Expose lines as read-only to prevent external mutation bypassing the aggregate boundary.
    /// </summary>
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    // Private parameterless constructor for ORM reconstitution
    private Order() { }

    private Order(Guid id, string customerName, IEnumerable<OrderLine> lines, DateTime createdAtUtc)
    {
        Id = id;
        CustomerName = customerName;
        CreatedAtUtc = createdAtUtc;

        foreach (var line in lines)
        {
            _lines.Add(line);
        }

        CalculateTotal();
    }

    /// <summary>
    /// Factory method acting as the consistency boundary.
    /// An Order can NEVER exist in memory in an invalid state.
    /// </summary>
    public static Order Create(string customerName, IEnumerable<OrderLine> lines)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new DomainException("Customer name is required.");
        }

        var lineList = lines?.ToList() ?? new List<OrderLine>();
        if (lineList.Count == 0)
        {
            throw new DomainException("An order must contain at least one order line.");
        }

        return new Order(Guid.NewGuid(), customerName.Trim(), lineList, DateTime.UtcNow);
    }

    /// <summary>
    /// Intrinsic domain calculation: Total amount and bulk discounts are domain rules,
    /// not infrastructure or UI concerns.
    /// </summary>
    private void CalculateTotal()
    {
        var subtotal = _lines.Sum(l => l.Subtotal);

        // Business Rule: 10% discount for orders exceeding $100
        if (subtotal > 100m)
        {
            TotalAmount = subtotal * 0.90m;
        }
        else
        {
            TotalAmount = subtotal;
        }
    }
}
