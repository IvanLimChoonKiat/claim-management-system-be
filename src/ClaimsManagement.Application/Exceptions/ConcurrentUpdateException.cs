namespace ClaimsManagement.Application.Exceptions;

/// <summary>
/// Raised when a save fails due to an optimistic concurrency conflict (e.g. two officers racing
/// to assign the same claim). Mapped to 409 Conflict by the Api's exception middleware.
/// </summary>
public sealed class ConcurrentUpdateException : Exception
{
    public ConcurrentUpdateException(string message) : base(message)
    {
    }

    public ConcurrentUpdateException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
