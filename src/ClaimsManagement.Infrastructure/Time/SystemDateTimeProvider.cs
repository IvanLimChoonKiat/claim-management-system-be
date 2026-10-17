using ClaimsManagement.Application.Abstractions;

namespace ClaimsManagement.Infrastructure.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
