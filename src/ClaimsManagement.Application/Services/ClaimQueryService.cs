using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Exceptions;
using ClaimsManagement.Application.Mapping;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Exceptions;

namespace ClaimsManagement.Application.Services;

public sealed class ClaimQueryService(IClaimRepository claimRepository)
{
    /// <summary>
    /// Claimants may only view their own claim; claims officers may view any claim (they need to
    /// read an unassigned claim's details before deciding whether to pick it up). This ownership
    /// check is independent of any Angular route guard - see CLAUDE.md "Security standards".
    /// </summary>
    public async Task<ClaimDetailDto> GetForUserAsync(Guid claimId, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(claimId, cancellationToken)
            ?? throw new EntityNotFoundException($"Claim '{claimId}' was not found.");

        if (role == UserRole.Claimant && claim.ClaimantUserId != userId)
        {
            throw new ClaimOwnershipException("You do not have access to this claim.");
        }

        return claim.ToDetailDto();
    }

    public async Task<IReadOnlyList<ClaimSummaryDto>> GetMyClaimsAsync(Guid claimantUserId, CancellationToken cancellationToken)
    {
        var claims = await claimRepository.GetByClaimantAsync(claimantUserId, cancellationToken);
        return claims.Select(c => c.ToSummaryDto()).ToList();
    }

    public async Task<IReadOnlyList<ClaimSummaryDto>> GetUnassignedAsync(CancellationToken cancellationToken)
    {
        var claims = await claimRepository.GetUnassignedAsync(cancellationToken);
        return claims.Select(c => c.ToSummaryDto()).ToList();
    }

    public async Task<IReadOnlyList<ClaimSummaryDto>> GetOfficerWorkloadAsync(Guid officerUserId, CancellationToken cancellationToken)
    {
        var claims = await claimRepository.GetAssignedToOfficerAsync(officerUserId, cancellationToken);
        return claims.Select(c => c.ToSummaryDto()).ToList();
    }
}
