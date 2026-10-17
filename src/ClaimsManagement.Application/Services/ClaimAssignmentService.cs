using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Common;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Exceptions;
using ClaimsManagement.Application.Mapping;

namespace ClaimsManagement.Application.Services;

public sealed class ClaimAssignmentService(
    IClaimRepository claimRepository,
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher,
    IDateTimeProvider dateTimeProvider)
{
    public async Task<ClaimDetailDto> AssignToOfficerAsync(Guid claimId, Guid officerUserId, CancellationToken cancellationToken)
    {
        var claim = await claimRepository.GetByIdAsync(claimId, cancellationToken)
            ?? throw new EntityNotFoundException($"Claim '{claimId}' was not found.");

        claim.Assign(officerUserId, dateTimeProvider.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DomainEventDispatcher.PublishAndClearAsync(claim, eventPublisher, cancellationToken);

        return claim.ToDetailDto();
    }
}
