using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated request is missing a valid user id claim.");
    }

    public static UserRole GetUserRole(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.Role);

        return Enum.TryParse<UserRole>(value, out var role)
            ? role
            : throw new InvalidOperationException("Authenticated request is missing a valid role claim.");
    }
}
