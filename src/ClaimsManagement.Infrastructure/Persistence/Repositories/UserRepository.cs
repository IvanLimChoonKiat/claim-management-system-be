using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Persistence.Repositories;

public class UserRepository(ClaimsDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower(), cancellationToken);

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Users.Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);
}
