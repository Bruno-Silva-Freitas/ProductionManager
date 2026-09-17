using Microsoft.EntityFrameworkCore;
using ProductionManager.Domain.Entities;

namespace ProductionManager.Infrastructure.Persistence;

public sealed class ProductionDbContext(DbContextOptions<ProductionDbContext> options) : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Maquina> Maquinas => Set<Maquina>();
    public DbSet<OrdemProducao> Ordens => Set<OrdemProducao>();
    public DbSet<MetaHora> MetasHora => Set<MetaHora>();
    public DbSet<ApontamentoMetaHora> Apontamentos => Set<ApontamentoMetaHora>();
    public DbSet<ProgramacaoSemanal> Programacoes => Set<ProgramacaoSemanal>();
    public DbSet<ItemProgramacao> ItensProgramacao => Set<ItemProgramacao>();

    protected override void OnModelCreating(ModelBuilder model) =>
        model.ApplyConfigurationsFromAssembly(typeof(ProductionDbContext).Assembly);
}
