using monitor_access_agent_ms.Domain.Interfaces;
using monitor_access_agent_ms.Infrastructure.Persistence.Context;
using MicroservicesTemplate.Domain.Repositories;
using MicroservicesTemplate.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace monitor_access_agent_ms.Infrastructure.Repositories;


public class UnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private IDbContextTransaction? _transaction;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(DbContext context)
    {
        _context = context;
    }

    // Repositorio genérico cacheado por tipo
    public IBaseRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        var type = typeof(TEntity);

        if (!_repositories.ContainsKey(type))
            _repositories[type] = new BaseRepository<TEntity>(_context);

        return (IBaseRepository<TEntity>)_repositories[type];
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
        => _transaction = await _context.Database.BeginTransactionAsync(ct);

    public async Task CommitAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            if (_transaction != null)
                await _transaction.CommitAsync(ct);
        }
        catch
        {
            await RollbackAsync(ct);
            throw;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
            await _transaction.RollbackAsync(ct);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
            await _transaction.DisposeAsync();

        await _context.DisposeAsync();
    }
}
