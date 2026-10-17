using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Tests.TestDoubles;

namespace ClaimsManagement.Tests.Application;

public class DashboardServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    private static Claim NewClaim(ClaimType type = ClaimType.Motor, decimal? estimatedLiability = 1000m) => Claim.Submit(
        $"CLM-2026-{Guid.NewGuid():N}"[..17].ToUpperInvariant(), type, Guid.NewGuid(), Now.AddDays(-1),
        "Incident", "Location", type == ClaimType.Motor ? "ABC123" : null, type == ClaimType.Property ? "123 Main St" : null,
        estimatedLiability, Now);

    [Fact]
    public async Task GetSummaryAsync_WithNoClaims_ReturnsEmptySummary()
    {
        var service = new DashboardService(new InMemoryClaimRepository(), new InMemoryUserRepository());

        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(0, summary.TotalOpenClaims);
        Assert.Equal(0m, summary.OutstandingLiability);
        Assert.Empty(summary.ClaimsByStatus);
        Assert.Empty(summary.OfficerWorkloads);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsClaimsByStatus()
    {
        var repository = new InMemoryClaimRepository();
        var submitted = NewClaim();
        var assigned = NewClaim();
        assigned.Assign(Guid.NewGuid(), Now);
        repository.Add(submitted);
        repository.Add(assigned);
        var service = new DashboardService(repository, new InMemoryUserRepository());

        var summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(1, summary.ClaimsByStatus.Single(s => s.Status == ClaimStatus.Submitted).Count);
        Assert.Equal(1, summary.ClaimsByStatus.Single(s => s.Status == ClaimStatus.Assigned).Count);
    }

    [Fact]
    public async Task GetSummaryAsync_OutstandingLiability_SumsOnlyOpenClaims()
    {
        var repository = new InMemoryClaimRepository();
        var open = NewClaim(estimatedLiability: 1000m);
        var officerId = Guid.NewGuid();
        var approved = NewClaim(estimatedLiability: 5000m);
        approved.Assign(officerId, Now);
        approved.Decide(officerId, ClaimDecisionOutcome.Approved, 4500m, "Valid", Now);
        repository.Add(open);
        repository.Add(approved);
        var service = new DashboardService(repository, new InMemoryUserRepository());

        var summary = await service.GetSummaryAsync(CancellationToken.None);

        // Only the still-open claim's estimated liability counts as outstanding exposure -
        // the approved claim has already been resolved (settlement, not liability, applies).
        Assert.Equal(1000m, summary.OutstandingLiability);
        Assert.Equal(1, summary.TotalOpenClaims);
    }

    [Fact]
    public async Task GetSummaryAsync_GroupsWorkloadByOfficerWithDisplayName()
    {
        var repository = new InMemoryClaimRepository();
        var userRepository = new InMemoryUserRepository();
        var officer = User.Create("officer@example.com", "Bob Officer", UserRole.ClaimsOfficer, Now);
        userRepository.Add(officer);

        var claim1 = NewClaim();
        claim1.Assign(officer.Id, Now);
        var claim2 = NewClaim();
        claim2.Assign(officer.Id, Now);
        claim2.Decide(officer.Id, ClaimDecisionOutcome.Rejected, null, "Not covered", Now);

        repository.Add(claim1);
        repository.Add(claim2);
        var service = new DashboardService(repository, userRepository);

        var summary = await service.GetSummaryAsync(CancellationToken.None);

        var workload = Assert.Single(summary.OfficerWorkloads);
        Assert.Equal(officer.Id, workload.OfficerId);
        Assert.Equal("Bob Officer", workload.OfficerName);
        Assert.Equal(1, workload.ActiveCount);
        Assert.Equal(0, workload.ApprovedCount);
        Assert.Equal(1, workload.RejectedCount);
        Assert.Equal(1, workload.TotalHandled);
    }
}
