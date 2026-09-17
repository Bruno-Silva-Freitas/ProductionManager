using ProductionManager.Domain.Entities;

namespace ProductionManager.Application.Interfaces;

public interface IProdutoRepository
{
    Task<IReadOnlyList<Produto>> Listar(CancellationToken ct);
    Task<Produto?> Obter(int id, CancellationToken ct);
    Task<bool> ExistePN(string pn, CancellationToken ct);
    void Adicionar(Produto produto);
}

public interface IMaquinaRepository
{
    Task<IReadOnlyList<Maquina>> Listar(CancellationToken ct);
    Task<Maquina?> Obter(int id, CancellationToken ct);
    Task<bool> ExisteCodigo(string codigo, CancellationToken ct);
    void Adicionar(Maquina maquina);
}

public interface IOrdemProducaoRepository
{
    Task<OrdemProducao?> Obter(int id, CancellationToken ct);
    Task<OrdemProducao?> PorOF(string of, CancellationToken ct);
    Task<OrdemProducao?> PorMeta(int metaId, CancellationToken ct);
    Task<bool> ExisteOF(string of, CancellationToken ct);
    Task<bool> MaquinaEmProducao(int maquinaId, int excetoOrdemId, CancellationToken ct);
    void Adicionar(OrdemProducao ordem);
}

public interface IProgramacaoRepository
{
    Task<IReadOnlyList<ProgramacaoSemanal>> PorMaquina(int maquinaId, DateOnly? inicioSemana, CancellationToken ct);
    Task<ProgramacaoSemanal?> Obter(int id, CancellationToken ct);
    Task<bool> ExisteSemana(int maquinaId, DateOnly inicioSemana, CancellationToken ct);
    void Adicionar(ProgramacaoSemanal programacao);
}

public interface IUnitOfWork
{
    Task Salvar(CancellationToken ct);
}
