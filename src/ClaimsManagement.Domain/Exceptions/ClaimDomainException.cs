namespace ClaimsManagement.Domain.Exceptions;

public abstract class ClaimDomainException : Exception
{
    protected ClaimDomainException(string message) : base(message)
    {
    }
}
