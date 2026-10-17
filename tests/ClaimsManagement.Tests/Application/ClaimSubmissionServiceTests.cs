using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Exceptions;
using ClaimsManagement.Tests.TestDoubles;

namespace ClaimsManagement.Tests.Application;

public class ClaimSubmissionServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);

    private static ClaimSubmissionService CreateService(
        InMemoryClaimRepository repository, FakeUnitOfWork unitOfWork, RecordingEventPublisher publisher) =>
        new(repository, unitOfWork, publisher, new FixedClaimReferenceGenerator("CLM-2026-FIXED001"), new FixedDateTimeProvider(Now));

    [Fact]
    public async Task SubmitAsync_WithValidRequest_PersistsAndPublishesClaimSubmittedEvent()
    {
        var repository = new InMemoryClaimRepository();
        var unitOfWork = new FakeUnitOfWork();
        var publisher = new RecordingEventPublisher();
        var service = CreateService(repository, unitOfWork, publisher);
        var claimantId = Guid.NewGuid();

        var request = new SubmitClaimRequest(ClaimType.Motor, Now.AddDays(-1), "Incident", "Location", "ABC123", null, 1000m);
        var result = await service.SubmitAsync(claimantId, request, CancellationToken.None);

        Assert.Equal("CLM-2026-FIXED001", result.ClaimReference);
        Assert.Equal(ClaimStatus.Submitted, result.Status);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.NotNull(await repository.GetByIdAsync(result.Id, CancellationToken.None));
        Assert.Single(publisher.PublishedEvents);
    }

    [Fact]
    public async Task SubmitAsync_WithInvalidRequest_ThrowsAndDoesNotPersistOrPublish()
    {
        var repository = new InMemoryClaimRepository();
        var unitOfWork = new FakeUnitOfWork();
        var publisher = new RecordingEventPublisher();
        var service = CreateService(repository, unitOfWork, publisher);

        var request = new SubmitClaimRequest(ClaimType.Motor, Now.AddDays(-1), "Incident", "Location", null, null, null);

        await Assert.ThrowsAsync<ClaimValidationException>(() => service.SubmitAsync(Guid.NewGuid(), request, CancellationToken.None));
        Assert.Equal(0, unitOfWork.SaveCount);
        Assert.Empty(publisher.PublishedEvents);
    }
}
