namespace ClaimsManagement.Domain.Exceptions;

/// <summary>Thrown when a user attempts an action on a claim they do not own or are not assigned to.</summary>
public sealed class ClaimOwnershipException : ClaimDomainException
{
    public ClaimOwnershipException(string message) : base(message)
    {
    }
}
