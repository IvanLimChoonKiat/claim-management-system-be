using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Application.Abstractions;

/// <summary>Narrow repository for the Claim aggregate root - not a generic IRepository&lt;T&gt;.</summary>
public interface IClaimRepository
{
    Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Claim?> GetByReferenceAsync(string claimReference, CancellationToken cancellationToken);
    Task<IReadOnlyList<Claim>> GetByClaimantAsync(Guid claimantUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Claim>> GetUnassignedAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Claim>> GetAssignedToOfficerAsync(Guid officerUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken);
    void Add(Claim claim);
}
