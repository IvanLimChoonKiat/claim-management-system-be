using ClaimsManagement.Domain.Enums;

namespace ClaimsManagement.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private User()
    {
    }

    public static User Create(string email, string displayName, UserRole role, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.", nameof(displayName));
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            Role = role,
            CreatedAtUtc = nowUtc
        };
    }
}
