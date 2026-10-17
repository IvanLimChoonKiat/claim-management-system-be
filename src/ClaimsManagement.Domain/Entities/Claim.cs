using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Events;
using ClaimsManagement.Domain.Exceptions;

namespace ClaimsManagement.Domain.Entities;

/// <summary>
/// Aggregate root for the claims lifecycle. Owns the state machine (see docs/ARCHITECTURE.md) and
/// is the single place that decides which transitions are legal. Who is allowed to invoke a
/// transition (role/ownership) is also enforced here, since it is a business rule, not an HTTP
/// concern - the Application layer orchestrates but does not duplicate these checks.
/// </summary>
public class Claim
{
    private static readonly IReadOnlyDictionary<ClaimStatus, ClaimStatus[]> ValidTransitions = new Dictionary<ClaimStatus, ClaimStatus[]>
    {
        [ClaimStatus.Submitted] = [ClaimStatus.Assigned],
        [ClaimStatus.Assigned] = [ClaimStatus.InfoRequested, ClaimStatus.Approved, ClaimStatus.Rejected],
        [ClaimStatus.InfoRequested] = [ClaimStatus.Assigned],
        [ClaimStatus.Approved] = [],
        [ClaimStatus.Rejected] = []
    };

    public Guid Id { get; private set; }
    public string ClaimReference { get; private set; } = default!;
    public ClaimType Type { get; private set; }
    public Guid ClaimantUserId { get; private set; }
    public ClaimStatus Status { get; private set; }
    public DateTime IncidentDateUtc { get; private set; }
    public string IncidentDescription { get; private set; } = default!;
    public string IncidentLocation { get; private set; } = default!;
    public string? VehicleRegistration { get; private set; }
    public string? PropertyAddress { get; private set; }
    public Guid? AssignedOfficerUserId { get; private set; }
    public decimal? EstimatedLiability { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Optimistic concurrency token. SQLite has no native rowversion - see docs/ARCHITECTURE.md.</summary>
    public int Version { get; private set; }

    private readonly List<ClaimStatusHistory> _statusHistory = [];
    public IReadOnlyCollection<ClaimStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private readonly List<InformationRequest> _informationRequests = [];
    public IReadOnlyCollection<InformationRequest> InformationRequests => _informationRequests.AsReadOnly();

    public ClaimDecision? Decision { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private Claim()
    {
        // EF Core materialization
    }

    public static Claim Submit(
        string claimReference,
        ClaimType type,
        Guid claimantUserId,
        DateTime incidentDateUtc,
        string incidentDescription,
        string incidentLocation,
        string? vehicleRegistration,
        string? propertyAddress,
        decimal? estimatedLiability,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(claimReference))
        {
            throw new ArgumentException("Claim reference is required.", nameof(claimReference));
        }

        if (string.IsNullOrWhiteSpace(incidentDescription))
        {
            throw new ClaimValidationException("Incident description is required.");
        }

        if (string.IsNullOrWhiteSpace(incidentLocation))
        {
            throw new ClaimValidationException("Incident location is required.");
        }

        if (incidentDateUtc > nowUtc)
        {
            throw new ClaimValidationException("Incident date cannot be in the future.");
        }

        if (type == ClaimType.Motor && string.IsNullOrWhiteSpace(vehicleRegistration))
        {
            throw new ClaimValidationException("Vehicle registration is required for a motor claim.");
        }

        if (type == ClaimType.Property && string.IsNullOrWhiteSpace(propertyAddress))
        {
            throw new ClaimValidationException("Property address is required for a property claim.");
        }

        if (estimatedLiability is < 0)
        {
            throw new ClaimValidationException("Estimated liability cannot be negative.");
        }

        var claim = new Claim
        {
            Id = Guid.NewGuid(),
            ClaimReference = claimReference,
            Type = type,
            ClaimantUserId = claimantUserId,
            Status = ClaimStatus.Submitted,
            IncidentDateUtc = incidentDateUtc,
            IncidentDescription = incidentDescription.Trim(),
            IncidentLocation = incidentLocation.Trim(),
            VehicleRegistration = vehicleRegistration?.Trim(),
            PropertyAddress = propertyAddress?.Trim(),
            EstimatedLiability = estimatedLiability,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            Version = 1
        };

        claim._statusHistory.Add(ClaimStatusHistory.Record(claim.Id, null, ClaimStatus.Submitted, claimantUserId, nowUtc, "Claim submitted"));
        claim._domainEvents.Add(new ClaimSubmittedDomainEvent(Guid.NewGuid(), claim.Id, claim.ClaimReference, claim.Type, claim.ClaimantUserId, nowUtc));

        return claim;
    }

    public void Assign(Guid officerUserId, DateTime nowUtc)
    {
        EnsureTransitionAllowed(ClaimStatus.Assigned);

        AssignedOfficerUserId = officerUserId;
        RecordTransition(ClaimStatus.Assigned, officerUserId, nowUtc, "Claim assigned to officer");
        _domainEvents.Add(new ClaimAssignedDomainEvent(Guid.NewGuid(), Id, officerUserId, nowUtc));
    }

    public InformationRequest RequestInformation(Guid officerUserId, string requestText, DateTime nowUtc)
    {
        EnsureAssignedOfficer(officerUserId);
        EnsureTransitionAllowed(ClaimStatus.InfoRequested);

        var request = InformationRequest.Create(Id, officerUserId, requestText, nowUtc);
        _informationRequests.Add(request);
        RecordTransition(ClaimStatus.InfoRequested, officerUserId, nowUtc, "Additional information requested");
        _domainEvents.Add(new AdditionalInformationRequestedDomainEvent(Guid.NewGuid(), Id, request.Id, officerUserId, request.RequestText, nowUtc));

        return request;
    }

    public void RespondToInformationRequest(Guid claimantUserId, Guid informationRequestId, string responseText, DateTime nowUtc)
    {
        EnsureOwningClaimant(claimantUserId);
        EnsureTransitionAllowed(ClaimStatus.Assigned);

        var request = _informationRequests.FirstOrDefault(r => r.Id == informationRequestId)
            ?? throw new ClaimValidationException("Information request not found on this claim.");

        request.Respond(responseText, nowUtc);
        RecordTransition(ClaimStatus.Assigned, claimantUserId, nowUtc, "Claimant responded to information request");
    }

    public ClaimDecision Decide(Guid officerUserId, ClaimDecisionOutcome outcome, decimal? settlementAmount, string reason, DateTime nowUtc)
    {
        EnsureAssignedOfficer(officerUserId);

        var targetStatus = outcome == ClaimDecisionOutcome.Approved ? ClaimStatus.Approved : ClaimStatus.Rejected;
        EnsureTransitionAllowed(targetStatus);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ClaimValidationException("A reason is required when deciding a claim.");
        }

        if (outcome == ClaimDecisionOutcome.Approved)
        {
            if (settlementAmount is null or <= 0)
            {
                throw new ClaimValidationException("A positive settlement amount is required to approve a claim.");
            }
        }

        var decision = ClaimDecision.Create(Id, officerUserId, outcome, settlementAmount, reason, nowUtc);
        Decision = decision;
        RecordTransition(targetStatus, officerUserId, nowUtc, $"Claim {outcome.ToString().ToLowerInvariant()}");

        _domainEvents.Add(outcome == ClaimDecisionOutcome.Approved
            ? new ClaimApprovedDomainEvent(Guid.NewGuid(), Id, officerUserId, settlementAmount!.Value, nowUtc)
            : new ClaimRejectedDomainEvent(Guid.NewGuid(), Id, officerUserId, reason, nowUtc));

        return decision;
    }

    private void EnsureAssignedOfficer(Guid officerUserId)
    {
        if (AssignedOfficerUserId != officerUserId)
        {
            throw new ClaimOwnershipException("Only the officer assigned to this claim can perform this action.");
        }
    }

    private void EnsureOwningClaimant(Guid claimantUserId)
    {
        if (ClaimantUserId != claimantUserId)
        {
            throw new ClaimOwnershipException("Only the claimant who submitted this claim can perform this action.");
        }
    }

    private void EnsureTransitionAllowed(ClaimStatus target)
    {
        if (!ValidTransitions[Status].Contains(target))
        {
            throw new InvalidClaimTransitionException(Status, target);
        }
    }

    private void RecordTransition(ClaimStatus target, Guid changedByUserId, DateTime nowUtc, string note)
    {
        var from = Status;
        Status = target;
        UpdatedAtUtc = nowUtc;
        Version++;
        _statusHistory.Add(ClaimStatusHistory.Record(Id, from, target, changedByUserId, nowUtc, note));
    }
}
