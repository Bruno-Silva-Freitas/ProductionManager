using ProductionManager.Application.DTOs;
using ProductionManager.Application.Exceptions;
using ProductionManager.Application.Interfaces;

namespace ProductionManager.Application.Services;

public sealed class ConsultaService(IProdutoRepository produtos, IMaquinaRepository maquinas,
    IOrdemProducaoRepository ordens, IProgramacaoRepository programacoes)
{
    public async Task<IReadOnlyList<ProdutoDto>> Produtos(CancellationToken ct = default) =>
        (await produtos.Listar(ct)).Select(p => p.Dto()).ToList();
    public async Task<IReadOnlyList<MaquinaDto>> Maquinas(CancellationToken ct = default) =>
        (await maquinas.Listar(ct)).Select(m => m.Dto()).ToList();
    public async Task<OrdemDto> Ordem(int id, CancellationToken ct = default) =>
        (await ordens.Obter(id, ct) ?? throw new NotFoundException("OF não encontrada.")).Dto();
    public async Task<OrdemDto> PorOF(string of, CancellationToken ct = default) =>
        (await ordens.PorOF(of.Trim().ToUpperInvariant(), ct) ?? throw new NotFoundException("OF não encontrada.")).Dto();
    public async Task<MetasHoraDto> Metas(int id, CancellationToken ct = default)
    {
        var ordem = await ordens.Obter(id, ct) ?? throw new NotFoundException("OF não encontrada.");
        return new(ordem.Id, ordem.Versao, ordem.MetasHora.OrderBy(m => m.Inicio).Select(m => m.Dto()).ToList());
    }
    public async Task<IReadOnlyList<ProgramacaoDto>> Programacao(int maquinaId, DateOnly? semana = null, CancellationToken ct = default)
    {
        _ = await maquinas.Obter(maquinaId, ct) ?? throw new NotFoundException("Máquina não encontrada.");
        var inicio = semana?.AddDays(-(((int)semana.Value.DayOfWeek + 6) % 7));
        return (await programacoes.PorMaquina(maquinaId, inicio, ct)).Select(p => p.Dto()).ToList();
    }
}
