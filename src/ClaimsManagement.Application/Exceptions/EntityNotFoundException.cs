namespace ClaimsManagement.Application.Exceptions;

/// <summary>Raised when a requested entity does not exist. Mapped to 404 by the Api's exception middleware.</summary>
public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string message) : base(message)
    {
    }
}
