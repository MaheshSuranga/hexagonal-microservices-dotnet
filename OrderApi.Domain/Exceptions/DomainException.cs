namespace OrderApi.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant or business rule is violated.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
