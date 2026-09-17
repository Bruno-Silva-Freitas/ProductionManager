using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductionManager.Application.Interfaces;
using ProductionManager.Application.Services;
using ProductionManager.Domain.Entities;
using ProductionManager.Infrastructure.Persistence;
using ProductionManager.Infrastructure.Repositories;

namespace ProductionManager.Tests;

internal sealed class TestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"productionmanager-test-{Guid.NewGuid():N}.db");
    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false, ForeignKeys = true }.ToString();
    public ProductionDbContext Open() => new(new DbContextOptionsBuilder<ProductionDbContext>().UseSqlite(ConnectionString).Options);
    public TestDatabase()
    {
        using var db = Open();
        db.Database.EnsureCreated();
    }
    public async Task<(int Ordem, int OutraOrdem, int Maquina, int Produto)> Seed()
    {
        await using var db = Open();
        var produto = new Produto("000PN-A", "Tampa");
        var maquina = new Maquina("INJ-04", "Injetora 04");
        var ordem = new OrdemProducao("000OF-01", produto, maquina, 1000);
        var outra = new OrdemProducao("000OF-02", produto, maquina, 1000);
        db.Ordens.AddRange(ordem, outra);
        await db.SaveChangesAsync();
        return (ordem.Id, outra.Id, maquina.Id, produto.Id);
    }
    public void Dispose()
    {
        // Somente os arquivos com nome exclusivo criados por esta instância de teste.
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
            if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
    }
    public static ProgramadorService Programador(ProductionDbContext db, Perfil perfil = Perfil.Programador) => new(
        new ProdutoRepository(db), new MaquinaRepository(db), new OrdemProducaoRepository(db),
        new ProgramacaoRepository(db), new UnitOfWork(db), new UsuarioTeste(perfil), TimeProvider.System);
    public static OperadorService Operador(ProductionDbContext db, Perfil perfil = Perfil.Operador) => new(
        new OrdemProducaoRepository(db), new UnitOfWork(db), new UsuarioTeste(perfil), TimeProvider.System);
    public static ConsultaService Consulta(ProductionDbContext db) => new(new ProdutoRepository(db),
        new MaquinaRepository(db), new OrdemProducaoRepository(db), new ProgramacaoRepository(db));

    private sealed record UsuarioTeste(Perfil? Perfil) : IUsuarioAtual { public string? UsuarioId => "teste"; }
}
