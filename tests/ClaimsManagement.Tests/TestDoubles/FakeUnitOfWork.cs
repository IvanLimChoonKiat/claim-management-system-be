using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Exceptions;

namespace ClaimsManagement.Tests.TestDoubles;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    /// <summary>Set true to simulate a concurrency conflict on the next save - for ClaimAssignmentService tests etc.</summary>
    public bool ThrowConcurrencyExceptionOnNextSave { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ThrowConcurrencyExceptionOnNextSave)
        {
            ThrowConcurrencyExceptionOnNextSave = false;
            throw new ConcurrentUpdateException("Simulated concurrency conflict.");
        }

        SaveCount++;
        return Task.CompletedTask;
    }
}
