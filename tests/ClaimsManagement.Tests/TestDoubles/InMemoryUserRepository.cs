using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Domain.Entities;

namespace ClaimsManagement.Tests.TestDoubles;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly List<User> _users = [];

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Email == email.Trim().ToLowerInvariant()));

    public void Add(User user) => _users.Add(user);
}
