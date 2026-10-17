using ClaimsManagement.Domain.Entities;
using ClaimsManagement.Domain.Enums;
using ClaimsManagement.Domain.Events;
using ClaimsManagement.Domain.Exceptions;

namespace ClaimsManagement.Tests.Domain;

public class ClaimTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ClaimantId = Guid.NewGuid();
    private static readonly Guid OfficerId = Guid.NewGuid();
    private static readonly Guid OtherOfficerId = Guid.NewGuid();

    private static Claim SubmitMotorClaim() => Claim.Submit(
        "CLM-2026-TEST0001", ClaimType.Motor, ClaimantId, Now.AddDays(-1),
        "Rear-ended at a traffic light", "Orchard Road", "SGX1234A", null, 5000m, Now);

    // --- Submission -----------------------------------------------------

    [Fact]
    public void Submit_WithValidMotorClaimData_CreatesSubmittedClaim()
    {
        var claim = SubmitMotorClaim();

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(ClaimType.Motor, claim.Type);
        Assert.Equal(ClaimantId, claim.ClaimantUserId);
        Assert.Null(claim.AssignedOfficerUserId);
        Assert.Single(claim.StatusHistory);
        Assert.Equal(ClaimStatus.Submitted, claim.StatusHistory.Single().ToStatus);
        Assert.Null(claim.StatusHistory.Single().FromStatus);
    }

    [Fact]
    public void Submit_WithValidPropertyClaimData_CreatesSubmittedClaim()
    {
        var claim = Claim.Submit(
            "CLM-2026-TEST0002", ClaimType.Property, ClaimantId, Now.AddDays(-1),
            "Water damage from burst pipe", "123 Main St", null, "123 Main St", 2000m, Now);

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Equal(ClaimType.Property, claim.Type);
    }

    [Fact]
    public void Submit_RaisesClaimSubmittedDomainEvent()
    {
        var claim = SubmitMotorClaim();

        var domainEvent = Assert.Single(claim.DomainEvents);
        var submitted = Assert.IsType<ClaimSubmittedDomainEvent>(domainEvent);
        Assert.Equal(claim.Id, submitted.ClaimId);
        Assert.Equal(claim.ClaimReference, submitted.ClaimReference);
    }

    [Fact]
    public void Submit_WithEmptyIncidentDescription_ThrowsClaimValidationException()
    {
        Assert.Throws<ClaimValidationException>(() => Claim.Submit(
            "CLM-2026-TEST0003", ClaimType.Motor, ClaimantId, Now.AddDays(-1),
            "", "Orchard Road", "SGX1234A", null, null, Now));
    }

    [Fact]
    public void Submit_MotorClaimWithoutVehicleRegistration_ThrowsClaimValidationException()
    {
        Assert.Throws<ClaimValidationException>(() => Claim.Submit(
            "CLM-2026-TEST0004", ClaimType.Motor, ClaimantId, Now.AddDays(-1),
            "Incident", "Location", null, null, null, Now));
    }

    [Fact]
    public void Submit_PropertyClaimWithoutPropertyAddress_ThrowsClaimValidationException()
    {
        Assert.Throws<ClaimValidationException>(() => Claim.Submit(
            "CLM-2026-TEST0005", ClaimType.Property, ClaimantId, Now.AddDays(-1),
            "Incident", "Location", null, null, null, Now));
    }

    [Fact]
    public void Submit_WithFutureIncidentDate_ThrowsClaimValidationException()
    {
        Assert.Throws<ClaimValidationException>(() => Claim.Submit(
            "CLM-2026-TEST0006", ClaimType.Motor, ClaimantId, Now.AddDays(1),
            "Incident", "Location", "ABC123", null, null, Now));
    }

    [Fact]
    public void Submit_WithNegativeEstimatedLiability_ThrowsClaimValidationException()
    {
        Assert.Throws<ClaimValidationException>(() => Claim.Submit(
            "CLM-2026-TEST0007", ClaimType.Motor, ClaimantId, Now.AddDays(-1),
            "Incident", "Location", "ABC123", null, -100m, Now));
    }

    // --- Assignment -------------------------------------------------------

    [Fact]
    public void Assign_FromSubmitted_TransitionsToAssignedAndRaisesEvent()
    {
        var claim = SubmitMotorClaim();

        claim.Assign(OfficerId, Now);

        Assert.Equal(ClaimStatus.Assigned, claim.Status);
        Assert.Equal(OfficerId, claim.AssignedOfficerUserId);
        Assert.Contains(claim.DomainEvents, e => e is ClaimAssignedDomainEvent);
    }

    [Fact]
    public void Assign_WhenAlreadyAssigned_ThrowsInvalidClaimTransitionException()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        var ex = Assert.Throws<InvalidClaimTransitionException>(() => claim.Assign(OtherOfficerId, Now));
        Assert.Equal(ClaimStatus.Assigned, ex.CurrentStatus);
        Assert.Equal(ClaimStatus.Assigned, ex.AttemptedStatus);
    }

    // --- Information requests ---------------------------------------------

    [Fact]
    public void RequestInformation_ByAssignedOfficer_TransitionsToInfoRequested()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        var request = claim.RequestInformation(OfficerId, "Need the police report", Now);

        Assert.Equal(ClaimStatus.InfoRequested, claim.Status);
        Assert.Equal(InformationRequestStatus.Pending, request.Status);
        Assert.Contains(claim.DomainEvents, e => e is AdditionalInformationRequestedDomainEvent);
    }

    [Fact]
    public void RequestInformation_ByNonAssignedOfficer_ThrowsClaimOwnershipException()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        Assert.Throws<ClaimOwnershipException>(() => claim.RequestInformation(OtherOfficerId, "Need more info", Now));
    }

    [Fact]
    public void RequestInformation_WhenClaimNotYetAssigned_ThrowsInvalidClaimTransitionException()
    {
        var claim = SubmitMotorClaim();

        Assert.Throws<InvalidClaimTransitionException>(() => claim.RequestInformation(OfficerId, "Need more info", Now));
    }

    [Fact]
    public void RespondToInformationRequest_ByOwningClaimant_TransitionsBackToAssigned()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);
        var request = claim.RequestInformation(OfficerId, "Need the police report", Now);

        claim.RespondToInformationRequest(ClaimantId, request.Id, "Report #PR-123", Now);

        Assert.Equal(ClaimStatus.Assigned, claim.Status);
        Assert.Equal(InformationRequestStatus.Responded, claim.InformationRequests.Single().Status);
        Assert.Equal("Report #PR-123", claim.InformationRequests.Single().ResponseText);
    }

    [Fact]
    public void RespondToInformationRequest_ByNonOwningClaimant_ThrowsClaimOwnershipException()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);
        var request = claim.RequestInformation(OfficerId, "Need the police report", Now);

        Assert.Throws<ClaimOwnershipException>(() => claim.RespondToInformationRequest(Guid.NewGuid(), request.Id, "Report", Now));
    }

    [Fact]
    public void RespondToInformationRequest_WithUnknownRequestId_ThrowsClaimValidationException()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);
        claim.RequestInformation(OfficerId, "Need the police report", Now);

        Assert.Throws<ClaimValidationException>(() => claim.RespondToInformationRequest(ClaimantId, Guid.NewGuid(), "Report", Now));
    }

    // --- Decisions ----------------------------------------------------------

    [Fact]
    public void Decide_ApproveByAssignedOfficerWithSettlement_TransitionsToApproved()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        var decision = claim.Decide(OfficerId, ClaimDecisionOutcome.Approved, 4500m, "Valid claim", Now);

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(4500m, decision.SettlementAmount);
        Assert.Contains(claim.DomainEvents, e => e is ClaimApprovedDomainEvent);
    }

    [Fact]
    public void Decide_RejectByAssignedOfficer_TransitionsToRejected()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        claim.Decide(OfficerId, ClaimDecisionOutcome.Rejected, null, "Insufficient evidence", Now);

        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Contains(claim.DomainEvents, e => e is ClaimRejectedDomainEvent);
    }

    [Fact]
    public void Decide_ApproveWithoutSettlementAmount_ThrowsClaimValidationException()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        Assert.Throws<ClaimValidationException>(() => claim.Decide(OfficerId, ClaimDecisionOutcome.Approved, null, "Valid claim", Now));
    }

    [Fact]
    public void Decide_ByNonAssignedOfficer_ThrowsClaimOwnershipException()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        Assert.Throws<ClaimOwnershipException>(() => claim.Decide(OtherOfficerId, ClaimDecisionOutcome.Rejected, null, "Reason", Now));
    }

    [Fact]
    public void Decide_WhenAlreadyApproved_ThrowsInvalidClaimTransitionException()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);
        claim.Decide(OfficerId, ClaimDecisionOutcome.Approved, 1000m, "Reason", Now);

        Assert.Throws<InvalidClaimTransitionException>(() => claim.Decide(OfficerId, ClaimDecisionOutcome.Rejected, null, "Reason", Now));
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllAccumulatedEvents()
    {
        var claim = SubmitMotorClaim();
        claim.Assign(OfficerId, Now);

        claim.ClearDomainEvents();

        Assert.Empty(claim.DomainEvents);
    }
}
