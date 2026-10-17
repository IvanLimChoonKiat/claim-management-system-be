using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Dtos;
using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Application.Services;

/// <summary>Backs the dev-only mock login - gets-or-creates a user by email. Not production auth.</summary>
public sealed class UserService(IUserRepository userRepository, IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
{
    public async Task<UserDto> GetOrCreateAsync(DevLoginRequest request, CancellationToken cancellationToken)
    {
        var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            return new UserDto(existing.Id, existing.Email, existing.DisplayName, existing.Role);
        }

        var user = User.Create(request.Email, request.DisplayName, request.Role, dateTimeProvider.UtcNow);
        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.Email, user.DisplayName, user.Role);
    }
}
