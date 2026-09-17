using ProductionManager.Application.Commands;
using ProductionManager.Application.DTOs;
using ProductionManager.Application.Exceptions;
using ProductionManager.Application.Interfaces;
using ProductionManager.Domain.Entities;
using ProductionManager.Domain.Enums;

namespace ProductionManager.Application.Services;

public sealed class OperadorService(IOrdemProducaoRepository ordens, IUnitOfWork uow, IUsuarioAtual usuario, TimeProvider relogio)
{
    public async Task<OrdemDto> AlterarStatus(int id, AlterarStatus comando, CancellationToken ct = default)
    {
        var ordem = await ObterParaAlterar(id, comando.Versao, ct);
        if (comando.Status == StatusOrdem.Producao && await ordens.MaquinaEmProducao(ordem.MaquinaId, id, ct))
            throw new ConflictException("Já existe outra OF em Produção nesta máquina. Pause ou finalize aquela OF primeiro.");
        ordem.AlterarStatusOperacional(comando.Status);
        await uow.Salvar(ct);
        return ordem.Dto();
    }

    public async Task<OrdemDto> AdicionarMeta(int id, CriarMetaHora comando, CancellationToken ct = default)
    {
        var ordem = await ObterParaAlterar(id, comando.Versao, ct);
        ordem.AdicionarMetaHora(comando.Inicio, comando.Fim, comando.MetaPlanejada);
        await uow.Salvar(ct);
        return ordem.Dto();
    }

    public Task<OrdemDto> Apontar(int metaId, ApontarMetaHora comando, CancellationToken ct = default) =>
        SalvarApontamento(metaId, comando, false, ct);
    public Task<OrdemDto> Corrigir(int metaId, ApontarMetaHora comando, CancellationToken ct = default) =>
        SalvarApontamento(metaId, comando, true, ct);

    private async Task<OrdemDto> SalvarApontamento(int metaId, ApontarMetaHora comando, bool correcao, CancellationToken ct)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Operador);
        var ordem = await ordens.PorMeta(metaId, ct) ?? throw new NotFoundException("Meta Hora não encontrada.");
        RegrasAplicacao.ExigirVersao(ordem.Versao, comando.Versao);
        var meta = ordem.MetasHora.Single(m => m.Id == metaId);
        if (correcao) ordem.CorrigirApontamento(meta, comando.QuantidadeBoa, comando.Refugo, relogio.GetUtcNow());
        else ordem.RegistrarApontamento(meta, comando.QuantidadeBoa, comando.Refugo, relogio.GetUtcNow());
        await uow.Salvar(ct);
        return ordem.Dto();
    }

    public async Task<OrdemDto> Finalizar(int id, FinalizarOrdem comando, CancellationToken ct = default)
    {
        var ordem = await ObterParaAlterar(id, comando.Versao, ct);
        ordem.Finalizar(relogio.GetUtcNow());
        await uow.Salvar(ct);
        return ordem.Dto();
    }

    private async Task<OrdemProducao> ObterParaAlterar(int id, long versao, CancellationToken ct)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Operador);
        var ordem = await ordens.Obter(id, ct) ?? throw new NotFoundException("OF não encontrada.");
        RegrasAplicacao.ExigirVersao(ordem.Versao, versao);
        return ordem;
    }
}
