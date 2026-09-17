using Microsoft.EntityFrameworkCore;
using ProductionManager.Application.Interfaces;
using ProductionManager.Domain.Entities;
using ProductionManager.Domain.Enums;
using ProductionManager.Infrastructure.Persistence;

namespace ProductionManager.Infrastructure.Repositories;

public sealed class ProdutoRepository(ProductionDbContext db) : IProdutoRepository
{
    public async Task<IReadOnlyList<Produto>> Listar(CancellationToken ct) => await db.Produtos.OrderBy(p => p.PN).ToListAsync(ct);
    public Task<Produto?> Obter(int id, CancellationToken ct) => db.Produtos.SingleOrDefaultAsync(p => p.Id == id, ct);
    public Task<bool> ExistePN(string pn, CancellationToken ct) => db.Produtos.AnyAsync(p => p.PN == pn, ct);
    public void Adicionar(Produto produto) => db.Produtos.Add(produto);
}
public sealed class MaquinaRepository(ProductionDbContext db) : IMaquinaRepository
{
    public async Task<IReadOnlyList<Maquina>> Listar(CancellationToken ct) => await db.Maquinas.OrderBy(m => m.Codigo).ToListAsync(ct);
    public Task<Maquina?> Obter(int id, CancellationToken ct) => db.Maquinas.SingleOrDefaultAsync(m => m.Id == id, ct);
    public Task<bool> ExisteCodigo(string codigo, CancellationToken ct) => db.Maquinas.AnyAsync(m => m.Codigo == codigo, ct);
    public void Adicionar(Maquina maquina) => db.Maquinas.Add(maquina);
}
public sealed class OrdemProducaoRepository(ProductionDbContext db) : IOrdemProducaoRepository
{
    private IQueryable<OrdemProducao> Completa => db.Ordens.Include(o => o.Produto).Include(o => o.Maquina)
        .Include(o => o.MetasHora).ThenInclude(m => m.Apontamento);
    public Task<OrdemProducao?> Obter(int id, CancellationToken ct) => Completa.SingleOrDefaultAsync(o => o.Id == id, ct);
    public Task<OrdemProducao?> PorOF(string of, CancellationToken ct) => Completa.SingleOrDefaultAsync(o => o.OF == of, ct);
    public Task<OrdemProducao?> PorMeta(int metaId, CancellationToken ct) => Completa.SingleOrDefaultAsync(o => o.MetasHora.Any(m => m.Id == metaId), ct);
    public Task<bool> ExisteOF(string of, CancellationToken ct) => db.Ordens.AnyAsync(o => o.OF == of, ct);
    public Task<bool> MaquinaEmProducao(int maquinaId, int excetoOrdemId, CancellationToken ct) =>
        db.Ordens.AnyAsync(o => o.MaquinaId == maquinaId && o.Id != excetoOrdemId && o.Status == StatusOrdem.Producao, ct);
    public void Adicionar(OrdemProducao ordem) => db.Ordens.Add(ordem);
}
public sealed class ProgramacaoRepository(ProductionDbContext db) : IProgramacaoRepository
{
    // Itens e metas são coleções aninhadas. A consulta única mantém versão, sequência
    // e resumos no mesmo snapshot, sem leituras separadas entre alterações concorrentes.
    private IQueryable<ProgramacaoSemanal> Completa => db.Programacoes.AsSingleQuery().Include(p => p.Maquina)
        .Include(p => p.Itens).ThenInclude(i => i.OrdemProducao).ThenInclude(o => o.Produto)
        .Include(p => p.Itens).ThenInclude(i => i.OrdemProducao).ThenInclude(o => o.Maquina)
        .Include(p => p.Itens).ThenInclude(i => i.OrdemProducao).ThenInclude(o => o.MetasHora).ThenInclude(m => m.Apontamento);
    public async Task<IReadOnlyList<ProgramacaoSemanal>> PorMaquina(int maquinaId, DateOnly? inicioSemana, CancellationToken ct) =>
        await Completa.Where(p => p.MaquinaId == maquinaId && (inicioSemana == null || p.InicioSemana == inicioSemana))
            .OrderByDescending(p => p.InicioSemana).ToListAsync(ct);
    public Task<ProgramacaoSemanal?> Obter(int id, CancellationToken ct) => Completa.SingleOrDefaultAsync(p => p.Id == id, ct);
    public Task<bool> ExisteSemana(int maquinaId, DateOnly inicioSemana, CancellationToken ct) =>
        db.Programacoes.AnyAsync(p => p.MaquinaId == maquinaId && p.InicioSemana == inicioSemana, ct);
    public void Adicionar(ProgramacaoSemanal programacao) => db.Programacoes.Add(programacao);
}
