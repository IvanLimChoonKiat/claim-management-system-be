using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Application.Services;

/// <summary>
/// Backs the team-wide dashboard: claims grouped by status, outstanding liability exposure, and
/// per-officer workload/performance. See DashboardSummaryDto for the role-visibility rationale.
/// </summary>
public sealed class DashboardService(IClaimRepository claimRepository, IUserRepository userRepository)
{
    private static readonly ClaimStatus[] OpenStatuses = [ClaimStatus.Submitted, ClaimStatus.Assigned, ClaimStatus.InfoRequested];

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var claims = await claimRepository.GetAllAsync(cancellationToken);

        var openClaims = claims.Where(c => OpenStatuses.Contains(c.Status)).ToList();
        var outstandingLiability = openClaims.Sum(c => c.EstimatedLiability ?? 0m);

        var claimsByStatus = claims
            .GroupBy(c => c.Status)
            .Select(g => new ClaimsByStatusDto(g.Key, g.Count()))
            .OrderBy(s => s.Status)
            .ToList();

        var officerIds = claims
            .Where(c => c.AssignedOfficerUserId.HasValue)
            .Select(c => c.AssignedOfficerUserId!.Value)
            .Distinct()
            .ToList();
        var officers = (await userRepository.GetByIdsAsync(officerIds, cancellationToken))
            .ToDictionary(u => u.Id, u => u.DisplayName);

        var officerWorkloads = claims
            .Where(c => c.AssignedOfficerUserId.HasValue)
            .GroupBy(c => c.AssignedOfficerUserId!.Value)
            .Select(g =>
            {
                var approved = g.Count(c => c.Status == ClaimStatus.Approved);
                var rejected = g.Count(c => c.Status == ClaimStatus.Rejected);
                var active = g.Count(c => OpenStatuses.Contains(c.Status));
                return new OfficerWorkloadSummaryDto(
                    g.Key,
                    officers.TryGetValue(g.Key, out var name) ? name : "Unknown officer",
                    active,
                    approved,
                    rejected,
                    approved + rejected);
            })
            .OrderByDescending(w => w.ActiveCount)
            .ToList();

        return new DashboardSummaryDto(openClaims.Count, outstandingLiability, claimsByStatus, officerWorkloads);
    }
}
