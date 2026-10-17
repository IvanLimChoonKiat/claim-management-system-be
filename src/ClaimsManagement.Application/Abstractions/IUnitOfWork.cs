namespace ClaimsManagement.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Persists pending changes. Implementations must translate a concurrency conflict (two
    /// officers racing to assign/decide the same claim) into <see cref="Exceptions.ConcurrentUpdateException"/>
    /// rather than leaking an EF Core-specific exception type into the Application layer.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
