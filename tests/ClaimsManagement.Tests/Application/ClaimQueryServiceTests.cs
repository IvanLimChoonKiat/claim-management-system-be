using ClaimsManagement.Application.Exceptions;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Exceptions;
using ClaimsManagement.Tests.TestDoubles;

namespace ClaimsManagement.Tests.Application;

public class ClaimQueryServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    private static Claim NewClaim(Guid claimantId) => Claim.Submit(
        "CLM-2026-TEST9301", ClaimType.Motor, claimantId, Now.AddDays(-1),
        "Incident", "Location", "ABC123", null, 1000m, Now);

    [Fact]
    public async Task GetForUserAsync_AsOwningClaimant_ReturnsClaim()
    {
        var repository = new InMemoryClaimRepository();
        var claimantId = Guid.NewGuid();
        var claim = NewClaim(claimantId);
        repository.Add(claim);
        var service = new ClaimQueryService(repository);

        var result = await service.GetForUserAsync(claim.Id, claimantId, UserRole.Claimant, CancellationToken.None);

        Assert.Equal(claim.Id, result.Id);
    }

    [Fact]
    public async Task GetForUserAsync_AsDifferentClaimant_ThrowsClaimOwnershipException()
    {
        var repository = new InMemoryClaimRepository();
        var claim = NewClaim(Guid.NewGuid());
        repository.Add(claim);
        var service = new ClaimQueryService(repository);

        await Assert.ThrowsAsync<ClaimOwnershipException>(() =>
            service.GetForUserAsync(claim.Id, Guid.NewGuid(), UserRole.Claimant, CancellationToken.None));
    }

    [Fact]
    public async Task GetForUserAsync_AsClaimsOfficer_CanViewAnyClaim()
    {
        var repository = new InMemoryClaimRepository();
        var claim = NewClaim(Guid.NewGuid());
        repository.Add(claim);
        var service = new ClaimQueryService(repository);

        var result = await service.GetForUserAsync(claim.Id, Guid.NewGuid(), UserRole.ClaimsOfficer, CancellationToken.None);

        Assert.Equal(claim.Id, result.Id);
    }

    [Fact]
    public async Task GetForUserAsync_WhenClaimDoesNotExist_ThrowsEntityNotFoundException()
    {
        var service = new ClaimQueryService(new InMemoryClaimRepository());

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.GetForUserAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Claimant, CancellationToken.None));
    }

    [Fact]
    public async Task GetUnassignedAsync_OnlyReturnsSubmittedClaims()
    {
        var repository = new InMemoryClaimRepository();
        var unassigned = NewClaim(Guid.NewGuid());
        var assigned = NewClaim(Guid.NewGuid());
        assigned.Assign(Guid.NewGuid(), Now);
        repository.Add(unassigned);
        repository.Add(assigned);
        var service = new ClaimQueryService(repository);

        var result = await service.GetUnassignedAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(unassigned.Id, result[0].Id);
    }
}
