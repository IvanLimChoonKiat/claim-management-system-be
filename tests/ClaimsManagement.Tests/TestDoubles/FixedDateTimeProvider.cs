using ClaimsManagement.Application.Abstractions;

namespace ClaimsManagement.Tests.TestDoubles;

public sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
}
