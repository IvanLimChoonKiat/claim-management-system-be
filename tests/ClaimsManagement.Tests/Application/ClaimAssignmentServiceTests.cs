using ClaimsManagement.Application.Exceptions;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Tests.TestDoubles;

namespace ClaimsManagement.Tests.Application;

public class ClaimAssignmentServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    private static Claim NewSubmittedClaim(Guid claimantId)
    {
        var claim = Claim.Submit(
            "CLM-2026-TEST9001", ClaimType.Motor, claimantId, Now.AddDays(-1),
            "Incident", "Location", "ABC123", null, 1000m, Now);

        // In the real flow, ClaimSubmissionService would already have published and cleared this
        // event in a prior request - clear it here so assertions below see only the event(s) this
        // test's own action produces, matching that real-world sequencing.
        claim.ClearDomainEvents();
        return claim;
    }

    [Fact]
    public async Task AssignToOfficerAsync_OnUnassignedClaim_Succeeds()
    {
        var repository = new InMemoryClaimRepository();
        var claim = NewSubmittedClaim(Guid.NewGuid());
        repository.Add(claim);
        var unitOfWork = new FakeUnitOfWork();
        var publisher = new RecordingEventPublisher();
        var service = new ClaimAssignmentService(repository, unitOfWork, publisher, new FixedDateTimeProvider(Now));
        var officerId = Guid.NewGuid();

        var result = await service.AssignToOfficerAsync(claim.Id, officerId, CancellationToken.None);

        Assert.Equal(ClaimStatus.Assigned, result.Status);
        Assert.Equal(officerId, result.AssignedOfficerUserId);
        Assert.Single(publisher.PublishedEvents);
    }

    [Fact]
    public async Task AssignToOfficerAsync_WhenClaimDoesNotExist_ThrowsEntityNotFoundException()
    {
        var service = new ClaimAssignmentService(
            new InMemoryClaimRepository(), new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            service.AssignToOfficerAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task AssignToOfficerAsync_OnConcurrentAssignmentConflict_ThrowsConcurrentUpdateException()
    {
        // Simulates two officers racing to assign the same claim: the second save hits the
        // optimistic concurrency check and must surface as ConcurrentUpdateException (-> 409),
        // not succeed silently and not leak the EF-specific exception type.
        var repository = new InMemoryClaimRepository();
        var claim = NewSubmittedClaim(Guid.NewGuid());
        repository.Add(claim);
        var unitOfWork = new FakeUnitOfWork { ThrowConcurrencyExceptionOnNextSave = true };
        var service = new ClaimAssignmentService(repository, unitOfWork, new RecordingEventPublisher(), new FixedDateTimeProvider(Now));

        await Assert.ThrowsAsync<ConcurrentUpdateException>(() =>
            service.AssignToOfficerAsync(claim.Id, Guid.NewGuid(), CancellationToken.None));
    }
}
