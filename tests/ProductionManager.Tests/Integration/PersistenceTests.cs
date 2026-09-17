using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductionManager.Application.Exceptions;
using ProductionManager.Domain.Entities;
using ProductionManager.Domain.Enums;
using ProductionManager.Infrastructure.Persistence;
using ProductionManager.Infrastructure.Repositories;

namespace ProductionManager.Tests.Integration;

public sealed class PersistenceTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 16, 23, 0, 0, TimeSpan.FromHours(-3));

    [Fact] public async Task ReabrirBancoPreservaResultadoFusoETimestamps()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using (var db = banco.Open())
        {
            var o = (await new OrdemProducaoRepository(db).Obter(ids.Ordem, default))!;
            var m = o.AdicionarMetaHora(Inicio, Inicio.AddHours(1), 500);
            o.AlterarStatusOperacional(StatusOrdem.Producao);
            o.RegistrarApontamento(m, 525, 25, Inicio.AddHours(1));
            o.CorrigirApontamento(m, 530, 20, Inicio.AddHours(2));
            o.Finalizar(Inicio.AddHours(3));
            await new UnitOfWork(db).Salvar(default);
        }
        await using var outro = banco.Open();
        var lida = (await new OrdemProducaoRepository(outro).Obter(ids.Ordem, default))!;
        var meta = Assert.Single(lida.MetasHora);
        Assert.Equal(Inicio, meta.Inicio); Assert.Equal(Inicio.Offset, meta.Inicio.Offset);
        Assert.Equal(Inicio.AddHours(2), meta.Apontamento!.AtualizadoEm);
        Assert.Equal(Inicio.AddHours(1), meta.Apontamento.RegistradoEm);
        Assert.Equal(530, lida.QuantidadeProduzida); Assert.Equal(20, lida.RefugoTotal);
        Assert.Equal(1.06m, lida.Eficiencia); Assert.Equal(530m / 550, lida.Qualidade);
        Assert.Equal(StatusOrdem.Finalizada, lida.Status); Assert.Equal(Inicio.AddHours(3), lida.FinalizadaEm);
        Assert.Equal(1, await outro.Apontamentos.CountAsync());
    }
    [Fact] public async Task ConcorrenciaMesmaOFReverteTambemInsercaoDaMeta()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var a = banco.Open(); await using var b = banco.Open();
        var oa = (await new OrdemProducaoRepository(a).Obter(ids.Ordem, default))!;
        var ob = (await new OrdemProducaoRepository(b).Obter(ids.Ordem, default))!;
        oa.AdicionarMetaHora(Inicio, Inicio.AddHours(1), 500);
        ob.AdicionarMetaHora(Inicio.AddMinutes(30), Inicio.AddHours(2), 500);
        await new UnitOfWork(a).Salvar(default);
        await Assert.ThrowsAsync<ConflictException>(() => new UnitOfWork(b).Salvar(default));
        await using var verificar = banco.Open();
        Assert.Equal(1, await verificar.MetasHora.CountAsync());
        Assert.Equal(2, (await verificar.Ordens.SingleAsync(o => o.Id == ids.Ordem)).Versao);
    }
    [Fact] public async Task IndiceDoBancoImpedeCorridaEntreDuasOFsDaMesmaMaquina()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var a = banco.Open(); await using var b = banco.Open();
        var ra = new OrdemProducaoRepository(a); var rb = new OrdemProducaoRepository(b);
        // Os dois clientes observam a máquina livre antes de gravar.
        Assert.False(await ra.MaquinaEmProducao(ids.Maquina, ids.Ordem, default));
        Assert.False(await rb.MaquinaEmProducao(ids.Maquina, ids.OutraOrdem, default));
        (await ra.Obter(ids.Ordem, default))!.AlterarStatusOperacional(StatusOrdem.Producao);
        (await rb.Obter(ids.OutraOrdem, default))!.AlterarStatusOperacional(StatusOrdem.Producao);
        await new UnitOfWork(a).Salvar(default);
        await Assert.ThrowsAsync<ConflictException>(() => new UnitOfWork(b).Salvar(default));
        await using var verificar = banco.Open();
        Assert.Equal(1, await verificar.Ordens.CountAsync(o => o.Status == StatusOrdem.Producao));
    }
    [Fact] public async Task BancoPossuiRestricaoUnicaPorMetaHora()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var db = banco.Open();
        var o = (await new OrdemProducaoRepository(db).Obter(ids.Ordem, default))!;
        var m = o.AdicionarMetaHora(Inicio, Inicio.AddHours(1), 500);
        o.AlterarStatusOperacional(StatusOrdem.Producao); o.RegistrarApontamento(m, 0, 0);
        await db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Apontamentos (MetaHoraId, QuantidadeBoa, Refugo, RegistradoEm) SELECT MetaHoraId, QuantidadeBoa, Refugo, RegistradoEm FROM Apontamentos"));
        Assert.Equal(19, ex.SqliteErrorCode);
        Assert.Equal(1, await db.Apontamentos.CountAsync());
    }
    [Theory][InlineData("PN")][InlineData("Maquina")][InlineData("OF")]
    public async Task RestricoesUnicasProtegemAteQuandoConsultaPreviaEIgnorada(string tipo)
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var db = banco.Open();
        if (tipo == "PN") db.Produtos.Add(new Produto("000PN-A", "Outra"));
        else if (tipo == "Maquina") db.Maquinas.Add(new Maquina("INJ-04", "Outra"));
        else db.Ordens.Add(new OrdemProducao("000OF-01", (await db.Produtos.FindAsync(ids.Produto))!, (await db.Maquinas.FindAsync(ids.Maquina))!, 1));
        await Assert.ThrowsAsync<ConflictException>(() => new UnitOfWork(db).Salvar(default));
    }
    [Fact] public async Task ConcorrenciaEmCorrecaoNaoPerdeApontamentoAnterior()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using (var seed = banco.Open())
        {
            var o = (await new OrdemProducaoRepository(seed).Obter(ids.Ordem, default))!;
            var m = o.AdicionarMetaHora(Inicio, Inicio.AddHours(1), 500);
            o.AlterarStatusOperacional(StatusOrdem.Producao); o.RegistrarApontamento(m, 100, 5);
            await seed.SaveChangesAsync();
        }
        await using var a = banco.Open(); await using var b = banco.Open();
        var oa = (await new OrdemProducaoRepository(a).Obter(ids.Ordem, default))!;
        var ob = (await new OrdemProducaoRepository(b).Obter(ids.Ordem, default))!;
        oa.CorrigirApontamento(Assert.Single(oa.MetasHora), 200, 10);
        ob.CorrigirApontamento(Assert.Single(ob.MetasHora), 300, 20);
        await new UnitOfWork(a).Salvar(default);
        await Assert.ThrowsAsync<ConflictException>(() => new UnitOfWork(b).Salvar(default));
        await using var check = banco.Open();
        Assert.Equal(200, (await check.Apontamentos.SingleAsync()).QuantidadeBoa);
    }

    [Fact] public async Task DuasMaquinasPodemProduzirAoMesmoTempo()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var db = banco.Open();
        var primeira = (await new OrdemProducaoRepository(db).Obter(ids.Ordem, default))!;
        var outra = new OrdemProducao("OF-M2", primeira.Produto, new Maquina("INJ-05", "Injetora 05"), 1000);
        primeira.AlterarStatusOperacional(StatusOrdem.Producao);
        outra.AlterarStatusOperacional(StatusOrdem.Producao);
        db.Ordens.Add(outra);
        await new UnitOfWork(db).Salvar(default);
        Assert.Equal(2, await db.Ordens.CountAsync(o => o.Status == StatusOrdem.Producao));
    }

    [Fact] public async Task ConcorrenciaDeSequenciaReverteItensDoSegundoCliente()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        int programacaoId;
        await using (var seed = banco.Open())
        {
            var p = await TestDatabase.Programador(seed).CriarProgramacao(ids.Maquina,
                new(new DateOnly(2026, 9, 14), [ids.Ordem, ids.OutraOrdem]));
            programacaoId = p.Id;
        }
        await using var a = banco.Open(); await using var b = banco.Open();
        var pa = (await new ProgramacaoRepository(a).Obter(programacaoId, default))!;
        var pb = (await new ProgramacaoRepository(b).Obter(programacaoId, default))!;
        pa.Reordenar([ids.OutraOrdem, ids.Ordem]);
        pb.Reordenar([ids.Ordem, ids.OutraOrdem]);
        await new UnitOfWork(a).Salvar(default);
        await Assert.ThrowsAsync<ConflictException>(() => new UnitOfWork(b).Salvar(default));
        await using var check = banco.Open();
        var final = Assert.Single(await TestDatabase.Consulta(check).Programacao(ids.Maquina));
        Assert.Equal(new[] { ids.OutraOrdem, ids.Ordem }, final.Itens.Select(i => i.Ordem.Id));
    }
}
