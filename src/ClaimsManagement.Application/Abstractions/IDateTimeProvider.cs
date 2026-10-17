namespace ClaimsManagement.Application.Abstractions;

/// <summary>Testability seam for "now" - lets unit tests control time deterministically.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
