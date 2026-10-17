using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Exceptions;
using ClaimsManagement.Tests.TestDoubles;

namespace ClaimsManagement.Tests.Application;

public class InformationRequestServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ClaimantId = Guid.NewGuid();
    private static readonly Guid OfficerId = Guid.NewGuid();

    private static (InMemoryClaimRepository Repository, Claim Claim) CreateAssignedClaim()
    {
        var repository = new InMemoryClaimRepository();
        var claim = Claim.Submit(
            "CLM-2026-TEST9101", ClaimType.Motor, ClaimantId, Now.AddDays(-1),
            "Incident", "Location", "ABC123", null, 1000m, Now);
        claim.Assign(OfficerId, Now);
        repository.Add(claim);
        return (repository, claim);
    }

    [Fact]
    public async Task RequestInformationAsync_ByAssignedOfficer_Succeeds()
    {
        var (repository, claim) = CreateAssignedClaim();
        var service = new InformationRequestService(repository, new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));

        var result = await service.RequestInformationAsync(claim.Id, OfficerId, new RequestInformationRequest("Need more info"), CancellationToken.None);

        Assert.Equal(ClaimStatus.InfoRequested, result.Status);
        Assert.Single(result.InformationRequests);
    }

    [Fact]
    public async Task RespondAsync_ByOwningClaimant_TransitionsBackToAssigned()
    {
        var (repository, claim) = CreateAssignedClaim();
        var requestService = new InformationRequestService(repository, new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));
        var requestResult = await requestService.RequestInformationAsync(claim.Id, OfficerId, new RequestInformationRequest("Need more info"), CancellationToken.None);
        var requestId = requestResult.InformationRequests.Single().Id;

        var result = await requestService.RespondAsync(claim.Id, ClaimantId, requestId, new RespondToInformationRequest("Here you go"), CancellationToken.None);

        Assert.Equal(ClaimStatus.Assigned, result.Status);
        Assert.Equal("Here you go", result.InformationRequests.Single().ResponseText);
    }

    [Fact]
    public async Task RespondAsync_ByNonOwningClaimant_ThrowsClaimOwnershipException()
    {
        var (repository, claim) = CreateAssignedClaim();
        var service = new InformationRequestService(repository, new FakeUnitOfWork(), new RecordingEventPublisher(), new FixedDateTimeProvider(Now));
        var requestResult = await service.RequestInformationAsync(claim.Id, OfficerId, new RequestInformationRequest("Need more info"), CancellationToken.None);
        var requestId = requestResult.InformationRequests.Single().Id;

        await Assert.ThrowsAsync<ClaimOwnershipException>(() =>
            service.RespondAsync(claim.Id, Guid.NewGuid(), requestId, new RespondToInformationRequest("Here you go"), CancellationToken.None));
    }
}
