using Microsoft.EntityFrameworkCore;
using MicroservicesTemplate.Domain.Repositories;
using MicroservicesTemplate.Infrastructure.Repositories;
using monitor_access_agent_ms.Domain.Interfaces;
using monitor_access_agent_ms.Infrastructure.Repositories;

namespace monitor_access_agent_ms.libs;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGapInfrastructure<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        return services;
    }
}