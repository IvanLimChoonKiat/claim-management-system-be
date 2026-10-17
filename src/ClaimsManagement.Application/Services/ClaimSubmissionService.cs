using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Common;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Mapping;
using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Services;

namespace ClaimsManagement.Application.Services;

public sealed class ClaimSubmissionService(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher,
    IClaimReferenceGenerator referenceGenerator,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<ClaimDetailDto> SubmitAsync(Guid claimantUserId, SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var reference = referenceGenerator.Generate(now);

        var claim = Claim.Submit(
            reference,
            request.Type,
            claimantUserId,
            request.IncidentDateUtc,
            request.IncidentDescription,
            request.IncidentLocation,
            request.VehicleRegistration,
            request.PropertyAddress,
            request.EstimatedLiability,
            now);

        claimRepository.Add(claim);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DomainEventDispatcher.PublishAndClearAsync(claim, eventPublisher, cancellationToken);

        return claim.ToDetailDto();
    }
}
