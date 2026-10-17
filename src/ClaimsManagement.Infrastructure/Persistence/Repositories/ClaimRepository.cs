using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Persistence.Repositories;

public class ClaimRepository(ClaimsDbContext dbContext) : IClaimRepository
{
    private IQueryable<Claim> TrackedClaimsWithGraph() =>
        dbContext.Claims
            .Include(c => c.StatusHistory)
            .Include(c => c.InformationRequests)
            .Include(c => c.Decision);

    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        TrackedClaimsWithGraph().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Claim?> GetByReferenceAsync(string claimReference, CancellationToken cancellationToken) =>
        TrackedClaimsWithGraph().FirstOrDefaultAsync(c => c.ClaimReference == claimReference, cancellationToken);

    public async Task<IReadOnlyList<Claim>> GetByClaimantAsync(Guid claimantUserId, CancellationToken cancellationToken) =>
        await TrackedClaimsWithGraph()
            .Where(c => c.ClaimantUserId == claimantUserId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Claim>> GetUnassignedAsync(CancellationToken cancellationToken) =>
        await TrackedClaimsWithGraph()
            .Where(c => c.Status == ClaimStatus.Submitted)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Claim>> GetAssignedToOfficerAsync(Guid officerUserId, CancellationToken cancellationToken) =>
        await TrackedClaimsWithGraph()
            .Where(c => c.AssignedOfficerUserId == officerUserId)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken) =>
        await TrackedClaimsWithGraph()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(Claim claim) => dbContext.Claims.Add(claim);
}
