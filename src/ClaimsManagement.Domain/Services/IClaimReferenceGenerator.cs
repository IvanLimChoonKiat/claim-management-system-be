namespace ClaimsManagement.Domain.Services;

public interface IClaimReferenceGenerator
{
    string Generate(DateTime nowUtc);
}

/// <summary>
/// GUID-suffix based reference (e.g. CLM-2026-A1B2C3D4) rather than a sequential counter -
/// avoids a race condition on "next sequence number" under concurrent submissions without
/// needing a database sequence/lock. Collision probability is negligible; the unique index on
/// ClaimReference is the safety net.
/// </summary>
public sealed class ClaimReferenceGenerator : IClaimReferenceGenerator
{
    public string Generate(DateTime nowUtc) =>
        $"CLM-{nowUtc:yyyy}-{Guid.NewGuid():N}"[..17].ToUpperInvariant();
}
