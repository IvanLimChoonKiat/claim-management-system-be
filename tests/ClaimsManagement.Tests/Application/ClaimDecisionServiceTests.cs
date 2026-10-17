using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Exceptions;
using ClaimsManagement.Tests.TestDoubles;

namespace ClaimsManagement.Tests.Application;

public class ClaimDecisionServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OfficerId = Guid.NewGuid();

    private static (InMemoryClaimRepository Repository, Claim Claim) CreateAssignedClaim()
    {
        var repository = new InMemoryClaimRepository();
        var claim = Claim.Submit(
            "CLM-2026-TEST9201", ClaimType.Motor, Guid.NewGuid(), Now.AddDays(-1),
            "Incident", "Location", "ABC123", null, 1000m, Now);
        claim.Assign(OfficerId, Now);
        // Both prior transitions would already have been published+cleared by the real submission
        // and assignment services in earlier requests - see ClaimAssignmentServiceTests.NewSubmittedClaim.
        claim.ClearDomainEvents();
        repository.Add(claim);
        return (repository, claim);
    }

    [Fact]
    public async Task DecideAsync_Approve_SetsStatusAndSettlementAmount()
    {
        var (repository, claim) = CreateAssignedClaim();
        var publisher = new RecordingEventPublisher();
        var service = new ClaimDecisionService(repository, new FakeUnitOfWork(), publisher, new FixedDateTimeProvider(Now));

        var result = await service.DecideAsync(
            claim.Id, OfficerId, new DecideClaimRequest(ClaimDecisionOutcome.Approved, 3000m, "Valid"), CancellationToken.None);

        Assert.Equal(ClaimStatus.Approved, result.Status);
        Assert.Equal(3000m, result.Decision!.SettlementAmount);
        Assert.Single(publisher.PublishedEvents);
    }

    [Fact]
    public async Task DecideAsync_Reject_SetsStatusWithoutSettlementAmount()
    {
        var (repository, claim) = CreateAssignedClaim();
        var service = new ClaimDecisionService(repository, new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));

        var result = await service.DecideAsync(
            claim.Id, OfficerId, new DecideClaimRequest(ClaimDecisionOutcome.Rejected, null, "Not covered"), CancellationToken.None);

        Assert.Equal(ClaimStatus.Rejected, result.Status);
        Assert.Null(result.Decision!.SettlementAmount);
    }

    [Fact]
    public async Task DecideAsync_ApproveWithoutSettlementAmount_ThrowsClaimValidationException()
    {
        var (repository, claim) = CreateAssignedClaim();
        var service = new ClaimDecisionService(repository, new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));

        await Assert.ThrowsAsync<ClaimValidationException>(() => service.DecideAsync(
            claim.Id, OfficerId, new DecideClaimRequest(ClaimDecisionOutcome.Approved, null, "Valid"), CancellationToken.None));
    }

    [Fact]
    public async Task DecideAsync_ByNonAssignedOfficer_ThrowsClaimOwnershipException()
    {
        var (repository, claim) = CreateAssignedClaim();
        var service = new ClaimDecisionService(repository, new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));

        await Assert.ThrowsAsync<ClaimOwnershipException>(() => service.DecideAsync(
            claim.Id, Guid.NewGuid(), new DecideClaimRequest(ClaimDecisionOutcome.Rejected, null, "Not covered"), CancellationToken.None));
    }

    [Fact]
    public async Task DecideAsync_WhenAlreadyDecided_ThrowsInvalidClaimTransitionException()
    {
        var (repository, claim) = CreateAssignedClaim();
        var service = new ClaimDecisionService(repository, new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));
        await service.DecideAsync(claim.Id, OfficerId, new DecideClaimRequest(ClaimDecisionOutcome.Approved, 100m, "Valid"), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidClaimTransitionException>(() => service.DecideAsync(
            claim.Id, OfficerId, new DecideClaimRequest(ClaimDecisionOutcome.Rejected, null, "Reconsidered"), CancellationToken.None));
    }
}
