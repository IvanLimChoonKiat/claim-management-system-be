namespace ClaimsManagement.Infrastructure.Messaging;

/// <summary>Topic names for the 5 claim lifecycle events. One topic per event type, versioned in the name.</summary>
public static class KafkaTopics
{
    public const string ClaimSubmitted = "claims.claim-submitted.v1";
    public const string ClaimAssigned = "claims.claim-assigned.v1";
    public const string AdditionalInformationRequested = "claims.additional-information-requested.v1";
    public const string ClaimApproved = "claims.claim-approved.v1";
    public const string ClaimRejected = "claims.claim-rejected.v1";

    public static readonly IReadOnlyList<string> All =
    [
        ClaimSubmitted,
        ClaimAssigned,
        AdditionalInformationRequested,
        ClaimApproved,
        ClaimRejected
    ];
}
