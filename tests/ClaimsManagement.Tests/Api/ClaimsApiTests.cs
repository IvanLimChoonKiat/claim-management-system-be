using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Tests.Api;

/// <summary>
/// End-to-end tests against a real (isolated) SQLite database via WebApplicationFactory - the
/// REST + persistence layer together, not just the Application services in isolation.
/// </summary>
public class ClaimsApiTests(ClaimsApiFactory factory) : IClassFixture<ClaimsApiFactory>
{
    // Mirrors the server's AddJsonOptions(JsonStringEnumConverter) - the test client needs its
    // own copy since HttpClient's JSON helpers don't inherit the server's serializer config.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(string Token, UserDto User)> LoginAsync(string email, UserRole role)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/dev-login", new DevLoginRequest(email, "Test User", role), JsonOptions);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DevLoginResponseDto>(JsonOptions);
        return (body!.Token, body.User);
    }

    private HttpRequestMessage Authorized(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }
        return request;
    }

    private async Task<ClaimDetailDto> SubmitClaimAsync(string token)
    {
        var request = new SubmitClaimRequest(ClaimType.Motor, DateTime.UtcNow.AddDays(-1), "Incident", "Location", "ABC123", null, 1000m);
        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/claims", token, request));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClaimDetailDto>(JsonOptions))!;
    }

    [Fact]
    public async Task FullLifecycle_SubmitAssignRequestRespondApprove_Succeeds()
    {
        var (claimantToken, _) = await LoginAsync($"claimant-{Guid.NewGuid():N}@example.com", UserRole.Claimant);
        var (officerToken, officer) = await LoginAsync($"officer-{Guid.NewGuid():N}@example.com", UserRole.ClaimsOfficer);

        var claim = await SubmitClaimAsync(claimantToken);
        Assert.Equal(ClaimStatus.Submitted, claim.Status);

        var assignResponse = await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/claims/{claim.Id}/assign", officerToken));
        assignResponse.EnsureSuccessStatusCode();
        var assigned = await assignResponse.Content.ReadFromJsonAsync<ClaimDetailDto>(JsonOptions);
        Assert.Equal(ClaimStatus.Assigned, assigned!.Status);
        Assert.Equal(officer.Id, assigned.AssignedOfficerUserId);

        var requestResponse = await _client.SendAsync(Authorized(
            HttpMethod.Post, $"/api/claims/{claim.Id}/information-requests", officerToken, new RequestInformationRequest("Need the police report")));
        requestResponse.EnsureSuccessStatusCode();
        var withRequest = await requestResponse.Content.ReadFromJsonAsync<ClaimDetailDto>(JsonOptions);
        Assert.Equal(ClaimStatus.InfoRequested, withRequest!.Status);
        var requestId = withRequest.InformationRequests.Single().Id;

        var respondResponse = await _client.SendAsync(Authorized(
            HttpMethod.Post, $"/api/claims/{claim.Id}/information-requests/{requestId}/respond", claimantToken, new RespondToInformationRequest("Report #123")));
        respondResponse.EnsureSuccessStatusCode();
        Assert.Equal(ClaimStatus.Assigned, (await respondResponse.Content.ReadFromJsonAsync<ClaimDetailDto>(JsonOptions))!.Status);

        var decideResponse = await _client.SendAsync(Authorized(
            HttpMethod.Post, $"/api/claims/{claim.Id}/decision", officerToken, new DecideClaimRequest(ClaimDecisionOutcome.Approved, 900m, "Valid claim")));
        decideResponse.EnsureSuccessStatusCode();
        var decided = await decideResponse.Content.ReadFromJsonAsync<ClaimDetailDto>(JsonOptions);
        Assert.Equal(ClaimStatus.Approved, decided!.Status);
        Assert.Equal(900m, decided.Decision!.SettlementAmount);
    }

    [Fact]
    public async Task Decide_WhenAlreadyDecided_Returns409Conflict()
    {
        var (claimantToken, _) = await LoginAsync($"claimant-{Guid.NewGuid():N}@example.com", UserRole.Claimant);
        var (officerToken, _) = await LoginAsync($"officer-{Guid.NewGuid():N}@example.com", UserRole.ClaimsOfficer);
        var claim = await SubmitClaimAsync(claimantToken);
        await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/claims/{claim.Id}/assign", officerToken));
        await _client.SendAsync(Authorized(
            HttpMethod.Post, $"/api/claims/{claim.Id}/decision", officerToken, new DecideClaimRequest(ClaimDecisionOutcome.Approved, 100m, "Valid")));

        var response = await _client.SendAsync(Authorized(
            HttpMethod.Post, $"/api/claims/{claim.Id}/decision", officerToken, new DecideClaimRequest(ClaimDecisionOutcome.Rejected, null, "Reconsidered")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Submit_WithMissingRequiredField_Returns400BadRequest()
    {
        var (claimantToken, _) = await LoginAsync($"claimant-{Guid.NewGuid():N}@example.com", UserRole.Claimant);
        var invalidRequest = new { type = "Motor", incidentDateUtc = DateTime.UtcNow.AddDays(-1), incidentDescription = "", incidentLocation = "Location" };

        var response = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/claims", claimantToken, invalidRequest));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUnassigned_AsClaimant_Returns403Forbidden()
    {
        var (claimantToken, _) = await LoginAsync($"claimant-{Guid.NewGuid():N}@example.com", UserRole.Claimant);

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/claims/unassigned", claimantToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMyClaims_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/claims/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OfficerWorkload_AfterAssignment_ReturnsTheAssignedClaim()
    {
        var (claimantToken, _) = await LoginAsync($"claimant-{Guid.NewGuid():N}@example.com", UserRole.Claimant);
        var (officerToken, officer) = await LoginAsync($"officer-{Guid.NewGuid():N}@example.com", UserRole.ClaimsOfficer);
        var claim = await SubmitClaimAsync(claimantToken);
        await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/claims/{claim.Id}/assign", officerToken));

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/officers/me/claims", officerToken));
        response.EnsureSuccessStatusCode();
        var workload = await response.Content.ReadFromJsonAsync<List<ClaimSummaryDto>>(JsonOptions);

        var workloadClaim = Assert.Single(workload!);
        Assert.Equal(claim.Id, workloadClaim.Id);
        Assert.Equal(officer.Id, workloadClaim.AssignedOfficerUserId);
    }

    [Fact]
    public async Task DashboardSummary_AfterAssignmentAndDecision_ReflectsTeamWorkloadAndLiability()
    {
        var (claimantToken, _) = await LoginAsync($"claimant-{Guid.NewGuid():N}@example.com", UserRole.Claimant);
        var (officerToken, officer) = await LoginAsync($"officer-{Guid.NewGuid():N}@example.com", UserRole.ClaimsOfficer);

        // Class-fixture db is shared across all tests in this class, so the dashboard's global
        // sums include other tests' data - baseline before/after and assert the delta, rather
        // than an absolute value, to keep this test order-independent.
        var before = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/dashboard/summary", officerToken));
        var baseline = (await before.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions))!;

        var openClaim = await SubmitClaimAsync(claimantToken);
        await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/claims/{openClaim.Id}/assign", officerToken));

        var decidedClaim = await SubmitClaimAsync(claimantToken);
        await _client.SendAsync(Authorized(HttpMethod.Post, $"/api/claims/{decidedClaim.Id}/assign", officerToken));
        await _client.SendAsync(Authorized(
            HttpMethod.Post, $"/api/claims/{decidedClaim.Id}/decision", officerToken,
            new DecideClaimRequest(ClaimDecisionOutcome.Approved, 900m, "Valid claim")));

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/dashboard/summary", officerToken));
        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions);

        var workload = summary!.OfficerWorkloads.Single(w => w.OfficerId == officer.Id);
        Assert.Equal(1, workload.ActiveCount);
        Assert.Equal(1, workload.ApprovedCount);
        // Only the still-open claim's estimated liability (1000, from SubmitClaimAsync) should
        // add to outstanding exposure - the approved claim is resolved, so it contributes 0.
        Assert.Equal(1000m, summary.OutstandingLiability - baseline.OutstandingLiability);
    }

    [Fact]
    public async Task DashboardSummary_AsClaimant_Returns403Forbidden()
    {
        var (claimantToken, _) = await LoginAsync($"claimant-{Guid.NewGuid():N}@example.com", UserRole.Claimant);

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/dashboard/summary", claimantToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetById_AsDifferentClaimant_Returns403Forbidden()
    {
        var (ownerToken, _) = await LoginAsync($"owner-{Guid.NewGuid():N}@example.com", UserRole.Claimant);
        var (otherToken, _) = await LoginAsync($"other-{Guid.NewGuid():N}@example.com", UserRole.Claimant);
        var claim = await SubmitClaimAsync(ownerToken);

        var response = await _client.SendAsync(Authorized(HttpMethod.Get, $"/api/claims/{claim.Id}", otherToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record DevLoginResponseDto(string Token, UserDto User);
}
