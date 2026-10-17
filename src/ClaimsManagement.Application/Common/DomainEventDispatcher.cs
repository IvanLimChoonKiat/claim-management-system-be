using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Application.Common;

/// <summary>
/// Publishes and clears a claim's accumulated domain events. Must only be called after
/// IUnitOfWork.SaveChangesAsync has succeeded - events describe facts that happened, so they must
/// never be published for a change that didn't actually persist.
/// </summary>
public static class DomainEventDispatcher
{
    public static async Task PublishAndClearAsync(Claim claim, IEventPublisher eventPublisher, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in claim.DomainEvents)
        {
            await eventPublisher.PublishAsync(domainEvent, cancellationToken);
        }

        claim.ClearDomainEvents();
    }
}
