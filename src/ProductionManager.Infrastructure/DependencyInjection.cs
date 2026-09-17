using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductionManager.Application.Interfaces;
using ProductionManager.Application.Services;
using ProductionManager.Infrastructure.Persistence;
using ProductionManager.Infrastructure.Repositories;

namespace ProductionManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductionManager(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ProductionDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IMaquinaRepository, MaquinaRepository>();
        services.AddScoped<IOrdemProducaoRepository, OrdemProducaoRepository>();
        services.AddScoped<IProgramacaoRepository, ProgramacaoRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ConsultaService>();
        services.AddScoped<ProgramadorService>();
        services.AddScoped<OperadorService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
