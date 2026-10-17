namespace ClaimsManagement.Domain.Exceptions;

/// <summary>Thrown when data provided to a domain operation violates a business invariant.</summary>
public sealed class ClaimValidationException : ClaimDomainException
{
    public ClaimValidationException(string message) : base(message)
    {
    }
}
