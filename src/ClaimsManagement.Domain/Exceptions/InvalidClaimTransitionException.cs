using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Exceptions;

/// <summary>Thrown when an operation would move a claim to a status not reachable from its current status.</summary>
public sealed class InvalidClaimTransitionException : ClaimDomainException
{
    public ClaimStatus CurrentStatus { get; }
    public ClaimStatus AttemptedStatus { get; }

    public InvalidClaimTransitionException(ClaimStatus currentStatus, ClaimStatus attemptedStatus)
        : base($"Cannot move claim from '{currentStatus}' to '{attemptedStatus}'.")
    {
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }
}
