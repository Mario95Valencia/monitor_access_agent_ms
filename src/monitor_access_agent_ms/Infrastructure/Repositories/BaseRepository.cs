using Microsoft.EntityFrameworkCore;
using MicroservicesTemplate.Domain.Repositories;
using monitor_access_agent_ms.Infrastructure.Persistence.Context;

namespace MicroservicesTemplate.Infrastructure.Repositories;

public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : class
{
    protected readonly DbContext _context;

    public BaseRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<TEntity?> GetByIdAsync(long id)
    {
        return await _context.Set<TEntity>().FindAsync(id);
    }

    public async Task<IEnumerable<TEntity>> GetAllAsync() => await _context.Set<TEntity>().ToListAsync();

    public async Task AddAsync(TEntity entity)
    {
        await _context.Set<TEntity>().AddAsync(entity);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync<TKey>(TKey id, TEntity entity)
    {
        var existingEntity = await _context.Set<TEntity>().FindAsync(id);

        if (existingEntity == null)
        {
            throw new KeyNotFoundException($"Entity with ID {id} not found.");
        }

        // Copiar propiedades de la entidad actualizada al objeto existente
        _context.Entry(existingEntity).CurrentValues.SetValues(entity);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _context.Set<TEntity>().Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    //Muestra solo los registros activos
    public async Task<IEnumerable<TEntity>> GetByStatusAsync(bool status)
    {
        return await _context.Set<TEntity>().Where(e => EF.Property<bool>(e, "Status") == status).ToListAsync();
    }

    // Cambia el estado de un registro a eliminado por bit
    public async Task SoftDeleteAsync(long id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            var propertyInfo = entity.GetType().GetProperty("Status");
            if (propertyInfo != null)
            {
                propertyInfo.SetValue(entity, false); // Cambia a false (bit) para indicar que está eliminado
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException("The entity does not contain a Status property.");
            }
        }
    }
}
