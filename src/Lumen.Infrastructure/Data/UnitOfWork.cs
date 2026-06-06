using Lumen.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace Lumen.Infrastructure.Data;

internal sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork, IAsyncDisposable
{
    private IDbContextTransaction? _transaction;

    public Task SaveChangesAsync(CancellationToken ct = default)
        => dbContext.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");

        _transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await operation(ct);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await _transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");

        _transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            T result = await operation(ct);
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await _transaction.CommitAsync(ct).ConfigureAwait(false);
            return result;
        }
        catch
        {
            await _transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
    }
}
