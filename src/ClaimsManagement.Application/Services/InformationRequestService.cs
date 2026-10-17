using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Common;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Exceptions;
using ClaimsManagement.Application.Mapping;

namespace ClaimsManagement.Application.Services;

public sealed class InformationRequestService(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<ClaimDetailDto> RequestInformationAsync(Guid claimId, Guid officerUserId, RequestInformationRequest request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(claimId, cancellationToken)
            ?? throw new EntityNotFoundException($"Claim '{claimId}' was not found.");

        claim.RequestInformation(officerUserId, request.RequestText, dateTimeProvider.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DomainEventDispatcher.PublishAndClearAsync(claim, eventPublisher, cancellationToken);

        return claim.ToDetailDto();
    }

    public async Task<ClaimDetailDto> RespondAsync(Guid claimId, Guid claimantUserId, Guid informationRequestId, RespondToInformationRequest request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(claimId, cancellationToken)
            ?? throw new EntityNotFoundException($"Claim '{claimId}' was not found.");

        claim.RespondToInformationRequest(claimantUserId, informationRequestId, request.ResponseText, dateTimeProvider.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return claim.ToDetailDto();
    }
}
