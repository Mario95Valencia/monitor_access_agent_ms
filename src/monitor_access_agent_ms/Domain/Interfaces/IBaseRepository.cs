namespace MicroservicesTemplate.Domain.Repositories
{
    public interface IBaseRepository<TEntity> where TEntity : class
    {
        Task<TEntity?> GetByIdAsync(long id);
        Task<IEnumerable<TEntity>> GetAllAsync();
        Task AddAsync(TEntity entity);
        Task UpdateAsync<TKey>(TKey id, TEntity entity);
        Task DeleteAsync(long id);
        Task<IEnumerable<TEntity>> GetByStatusAsync(bool status);
        Task SoftDeleteAsync(long id);


    }
}
