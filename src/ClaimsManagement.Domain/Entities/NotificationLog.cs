namespace ClaimsManagement.Domain.Entities;

/// <summary>
/// Written by the Kafka notification consumer (Infrastructure). Keyed by EventId so re-delivery
/// of an at-least-once event is a no-op - see docs/ARCHITECTURE.md "Delivery guarantees".
/// </summary>
public class NotificationLog
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid ClaimId { get; private set; }
    public string EventType { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    private NotificationLog()
    {
    }

    public static NotificationLog Create(Guid eventId, Guid claimId, string eventType, DateTime nowUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ClaimId = claimId,
            EventType = eventType,
            CreatedAtUtc = nowUtc
        };
}
