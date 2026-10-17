using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Exceptions;

namespace ClaimsManagement.Domain.Entities;

public class InformationRequest
{
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public Guid RequestedByOfficerUserId { get; private set; }
    public string RequestText { get; private set; } = default!;
    public DateTime RequestedAtUtc { get; private set; }
    public string? ResponseText { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }
    public InformationRequestStatus Status { get; private set; }

    private InformationRequest()
    {
    }

    internal static InformationRequest Create(Guid claimId, Guid officerUserId, string requestText, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(requestText))
        {
            throw new ClaimValidationException("Information request text is required.");
        }

        return new InformationRequest
        {
            Id = Guid.NewGuid(),
            ClaimId = claimId,
            RequestedByOfficerUserId = officerUserId,
            RequestText = requestText.Trim(),
            RequestedAtUtc = nowUtc,
            Status = InformationRequestStatus.Pending
        };
    }

    internal void Respond(string responseText, DateTime nowUtc)
    {
        if (Status != InformationRequestStatus.Pending)
        {
            throw new ClaimValidationException("This information request has already been responded to.");
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new ClaimValidationException("A response is required.");
        }

        ResponseText = responseText.Trim();
        RespondedAtUtc = nowUtc;
        Status = InformationRequestStatus.Responded;
    }
}
