namespace ClaimsManagement.Api.Auth;

/// <summary>
/// Dev-only mock auth signing options. SigningKey must come from user-secrets/environment outside
/// local development - the value in appsettings.Development.json is an intentionally fake
/// placeholder, never a real secret. See README.md "Known limitations".
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryHours { get; set; } = 8;
}
