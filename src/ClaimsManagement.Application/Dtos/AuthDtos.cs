using System.ComponentModel.DataAnnotations;
using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Application.Dtos;

/// <summary>
/// Dev-only mock login: identifies (or creates, on first use) a user by email and issues a token
/// for the given role. There is no password and no real identity verification - see README.md
/// "Known limitations". Never ship this endpoint reachable in a real Production environment.
/// </summary>
public sealed record DevLoginRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(200, MinimumLength = 1)] string DisplayName,
    UserRole Role);

public sealed record UserDto(Guid Id, string Email, string DisplayName, UserRole Role);
