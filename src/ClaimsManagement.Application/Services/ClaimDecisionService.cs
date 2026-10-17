using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Common;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Exceptions;
using ClaimsManagement.Application.Mapping;

namespace ClaimsManagement.Application.Services;

public sealed class ClaimDecisionService(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<ClaimDetailDto> DecideAsync(Guid claimId, Guid officerUserId, DecideClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(claimId, cancellationToken)
            ?? throw new EntityNotFoundException($"Claim '{claimId}' was not found.");

        claim.Decide(officerUserId, request.Outcome, request.SettlementAmount, request.Reason, dateTimeProvider.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DomainEventDispatcher.PublishAndClearAsync(claim, eventPublisher, cancellationToken);

        return claim.ToDetailDto();
    }
}
