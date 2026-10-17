using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Tests.TestDoubles;

public sealed class InMemoryClaimRepository : IClaimRepository
{
    private readonly List<Claim> _claims = [];

    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_claims.FirstOrDefault(c => c.Id == id));

    public Task<Claim?> GetByReferenceAsync(string claimReference, CancellationToken cancellationToken) =>
        Task.FromResult(_claims.FirstOrDefault(c => c.ClaimReference == claimReference));

    public Task<IReadOnlyList<Claim>> GetByClaimantAsync(Guid claimantUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Claim>>(_claims.Where(c => c.ClaimantUserId == claimantUserId).ToList());

    public Task<IReadOnlyList<Claim>> GetUnassignedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Claim>>(_claims.Where(c => c.Status == ClaimStatus.Submitted).ToList());

    public Task<IReadOnlyList<Claim>> GetAssignedToOfficerAsync(Guid officerUserId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Claim>>(_claims.Where(c => c.AssignedOfficerUserId == officerUserId).ToList());

    public Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Claim>>(_claims.ToList());

    public void Add(Claim claim) => _claims.Add(claim);
}
