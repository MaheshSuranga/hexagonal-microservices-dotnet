namespace OrderApi.Domain.Entities;

using OrderApi.Domain.Exceptions;

/// <summary>
/// Domain sub-entity/value object representing an item line inside an order.
/// Completely encapsulated: cannot be modified from outside its aggregate root.
/// </summary>
public class OrderLine
{
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal => Quantity * UnitPrice;

    // Parameterless constructor for ORM reconstitution
    private OrderLine() { }

    public OrderLine(string productName, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainException("Product name cannot be empty.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Unit price cannot be negative.");
        }

        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
