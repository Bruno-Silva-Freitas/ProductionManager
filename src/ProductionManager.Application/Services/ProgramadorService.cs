using ProductionManager.Application.Commands;
using ProductionManager.Application.DTOs;
using ProductionManager.Application.Exceptions;
using ProductionManager.Application.Interfaces;
using ProductionManager.Domain.Entities;
using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Application.Services;

public sealed class ProgramadorService(IProdutoRepository produtos, IMaquinaRepository maquinas,
    IOrdemProducaoRepository ordens, IProgramacaoRepository programacoes, IUnitOfWork uow,
    IUsuarioAtual usuario, TimeProvider relogio)
{
    public async Task<ProdutoDto> CriarProduto(CriarProduto comando, CancellationToken ct = default)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Programador);
        var produto = new Produto(comando.PN, comando.Nome);
        if (await produtos.ExistePN(produto.PN, ct)) throw new ConflictException("PN já cadastrado.");
        produtos.Adicionar(produto);
        await uow.Salvar(ct);
        return produto.Dto();
    }

    public async Task<MaquinaDto> CriarMaquina(CriarMaquina comando, CancellationToken ct = default)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Programador);
        var maquina = new Maquina(comando.Codigo, comando.Nome);
        if (await maquinas.ExisteCodigo(maquina.Codigo, ct)) throw new ConflictException("Código de máquina já cadastrado.");
        maquinas.Adicionar(maquina);
        await uow.Salvar(ct);
        return maquina.Dto();
    }

    public async Task<OrdemDto> CriarOrdem(CriarOrdem comando, CancellationToken ct = default)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Programador);
        RegrasAplicacao.ExigirId(comando.ProdutoId, "Produto");
        RegrasAplicacao.ExigirId(comando.MaquinaId, "Máquina");
        var produto = await produtos.Obter(comando.ProdutoId, ct) ?? throw new NotFoundException("Produto não encontrado.");
        var maquina = await maquinas.Obter(comando.MaquinaId, ct) ?? throw new NotFoundException("Máquina não encontrada.");
        var ordem = new OrdemProducao(comando.OF, produto, maquina, comando.QuantidadePlanejada, relogio.GetUtcNow());
        if (await ordens.ExisteOF(ordem.OF, ct)) throw new ConflictException("OF já cadastrada.");
        ordens.Adicionar(ordem);
        await uow.Salvar(ct);
        return ordem.Dto();
    }

    public async Task<ProgramacaoDto> CriarProgramacao(int maquinaId, CriarProgramacao comando, CancellationToken ct = default)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Programador);
        var maquina = await maquinas.Obter(maquinaId, ct) ?? throw new NotFoundException("Máquina não encontrada.");
        var programacao = new ProgramacaoSemanal(maquina, comando.InicioSemana);
        if (await programacoes.ExisteSemana(maquinaId, programacao.InicioSemana, ct))
            throw new ConflictException("Já existe programação desta máquina nesta semana. Adicione a OF à programação existente.");
        if (comando.OrdemIds is null) throw new DomainException("Informe a lista de OFs (pode estar vazia).");
        foreach (var id in comando.OrdemIds)
            programacao.AdicionarOrdem(await ordens.Obter(id, ct) ?? throw new NotFoundException($"OF {id} não encontrada."));
        programacoes.Adicionar(programacao);
        await uow.Salvar(ct);
        return programacao.Dto();
    }

    public async Task<ProgramacaoDto> AdicionarOrdem(int id, AdicionarOrdemProgramacao comando, CancellationToken ct = default)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Programador);
        var p = await programacoes.Obter(id, ct) ?? throw new NotFoundException("Programação não encontrada.");
        RegrasAplicacao.ExigirVersao(p.Versao, comando.Versao);
        p.AdicionarOrdem(await ordens.Obter(comando.OrdemId, ct) ?? throw new NotFoundException("OF não encontrada."));
        await uow.Salvar(ct);
        return p.Dto();
    }

    public async Task<ProgramacaoDto> Reordenar(int id, ReordenarProgramacao comando, CancellationToken ct = default)
    {
        RegrasAplicacao.ExigirPerfil(usuario, Perfil.Programador);
        var p = await programacoes.Obter(id, ct) ?? throw new NotFoundException("Programação não encontrada.");
        RegrasAplicacao.ExigirVersao(p.Versao, comando.Versao);
        p.Reordenar(comando.OrdemIds);
        await uow.Salvar(ct);
        return p.Dto();
    }
}
