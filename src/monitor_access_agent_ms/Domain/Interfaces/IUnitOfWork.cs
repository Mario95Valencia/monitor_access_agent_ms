using MicroservicesTemplate.Domain.Repositories;

namespace monitor_access_agent_ms.Domain.Interfaces;


/// <summary>
/// Esa interfaz utiliza UoW el cual crea la posibilidad de poder 
/// guardar parcialmente los procesos que necesite un ROLLBACK o similar,
/// además, permite guardado con transactions en caso de desconexiones.
/// Usarse segun convenga el caso
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IBaseRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
