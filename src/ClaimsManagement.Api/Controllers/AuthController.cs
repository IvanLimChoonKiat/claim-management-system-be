using ClaimsManagement.Api.Auth;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.Controllers;

/// <summary>
/// Dev-only mock authentication. No password, no real identity verification - issues a signed
/// JWT for whatever email/role the caller claims. NOT production authentication; see
/// README.md "Known limitations". This endpoint must never be reachable unguarded in Production.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserService userService, JwtTokenGenerator tokenGenerator) : ControllerBase
{
    [HttpPost("dev-login")]
    [AllowAnonymous]
    public async Task<ActionResult<DevLoginResponse>> DevLogin(DevLoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.GetOrCreateAsync(request, cancellationToken);
        var token = tokenGenerator.GenerateToken(user);
        return Ok(new DevLoginResponse(token, user));
    }
}

public sealed record DevLoginResponse(string Token, UserDto User);
