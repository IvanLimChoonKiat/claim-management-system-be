using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ClaimsManagement.Infrastructure.Persistence;

public class UnitOfWork(ClaimsDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate the EF-specific exception into an Application-level contract so Application
            // never needs to reference EF Core types directly (docs/ARCHITECTURE.md concurrency control).
            throw new ConcurrentUpdateException(
                "This claim was modified by someone else at the same time. Reload and try again.", ex);
        }
    }
}
