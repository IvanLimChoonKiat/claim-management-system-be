using ClaimsManagement.Domain.Services;

namespace ClaimsManagement.Tests.TestDoubles;

public sealed class FixedClaimReferenceGenerator(string reference) : IClaimReferenceGenerator
{
    public string Generate(DateTime nowUtc) => reference;
}
