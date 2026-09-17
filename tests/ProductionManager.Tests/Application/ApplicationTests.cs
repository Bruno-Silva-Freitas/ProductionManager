using Microsoft.EntityFrameworkCore;
using ProductionManager.Application.Commands;
using ProductionManager.Application.Exceptions;
using ProductionManager.Application.Interfaces;
using ProductionManager.Domain.Enums;
using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Tests.Application;

public sealed class ApplicationTests
{
    [Fact] public async Task CadastrosUnicosGlobalmenteIncluindoEspacosECaixa()
    {
        using var banco = new TestDatabase();
        await using var db = banco.Open();
        var s = TestDatabase.Programador(db);
        var p = await s.CriarProduto(new("000abc", "Peça"));
        var m = await s.CriarMaquina(new("inj-01", "Injetora"));
        await Assert.ThrowsAsync<ConflictException>(() => s.CriarProduto(new(" 000AbC ", "Outra")));
        await Assert.ThrowsAsync<ConflictException>(() => s.CriarMaquina(new(" INJ-01 ", "Outra")));
        var o = await s.CriarOrdem(new("000OF-a", p.Id, m.Id, 100));
        var m2 = await s.CriarMaquina(new("INJ-02", "Outra"));
        await Assert.ThrowsAsync<ConflictException>(() => s.CriarOrdem(new(" 000of-A ", p.Id, m2.Id, 100)));
        Assert.Equal("000OF-A", o.OF);
        Assert.Equal(o.Id, (await TestDatabase.Consulta(db).PorOF("000of-a")).Id);
    }
    [Fact] public async Task PerfisSaoVerificadosNaAplicacao()
    {
        using var banco = new TestDatabase();
        var ids = await banco.Seed();
        await using var db = banco.Open();
        await Assert.ThrowsAsync<ForbiddenException>(() => TestDatabase.Programador(db, Perfil.Operador).CriarProduto(new("PN", "Peça")));
        await Assert.ThrowsAsync<ForbiddenException>(() => TestDatabase.Programador(db, Perfil.Operador).CriarOrdem(new("OF", ids.Produto, ids.Maquina, 1)));
        await Assert.ThrowsAsync<ForbiddenException>(() => TestDatabase.Operador(db, Perfil.Programador).AlterarStatus(ids.Ordem, new(StatusOrdem.Producao, 1)));
        await Assert.ThrowsAsync<ForbiddenException>(() => TestDatabase.Operador(db, Perfil.Programador).Corrigir(1, new(1, 1, 1)));
    }
    [Fact] public async Task ApenasUmaOFEmProducaoPorMaquina()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var db = banco.Open(); var s = TestDatabase.Operador(db);
        var primeira = await s.AlterarStatus(ids.Ordem, new(StatusOrdem.Producao, 1));
        await Assert.ThrowsAsync<ConflictException>(() => s.AlterarStatus(ids.OutraOrdem, new(StatusOrdem.Producao, 1)));
        await s.AlterarStatus(ids.Ordem, new(StatusOrdem.Interrompida, primeira.Versao));
        var segunda = await s.AlterarStatus(ids.OutraOrdem, new(StatusOrdem.Producao, 1));
        Assert.Equal("Producao", segunda.Status);
    }
    [Fact] public async Task VersaoAusenteOuDesatualizadaNaoSobrescreveDados()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var db = banco.Open(); var s = TestDatabase.Operador(db);
        await Assert.ThrowsAsync<DomainException>(() => s.AlterarStatus(ids.Ordem, new(StatusOrdem.Producao, 0)));
        await s.AlterarStatus(ids.Ordem, new(StatusOrdem.Interrompida, 1));
        await Assert.ThrowsAsync<ConflictException>(() => s.AlterarStatus(ids.Ordem, new(StatusOrdem.Producao, 1)));
        Assert.Equal("Interrompida", (await TestDatabase.Consulta(db).Ordem(ids.Ordem)).Status);
    }
    [Fact] public async Task NaoEncontradoEReferenciasOmitidasTemErrosEspecificos()
    {
        using var banco = new TestDatabase(); await using var db = banco.Open();
        await Assert.ThrowsAsync<NotFoundException>(() => TestDatabase.Consulta(db).Ordem(999));
        await Assert.ThrowsAsync<NotFoundException>(() => TestDatabase.Operador(db).Apontar(999, new(0, 0, 1)));
        await Assert.ThrowsAsync<DomainException>(() => TestDatabase.Programador(db).CriarOrdem(new("O", 0, 1, 1)));
        await Assert.ThrowsAsync<NotFoundException>(() => TestDatabase.Programador(db).CriarOrdem(new("O", 999, 1, 1)));
    }
    [Fact] public async Task ProgramacaoPreservaSequenciaELiberaOFsNaSemana()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using (var db = banco.Open())
        {
            var s = TestDatabase.Programador(db);
            var p = await s.CriarProgramacao(ids.Maquina, new(new DateOnly(2026, 9, 16), [ids.Ordem, ids.OutraOrdem]));
            Assert.Equal(new DateOnly(2026, 9, 14), p.InicioSemana);
            Assert.Equal(new[] { ids.Ordem, ids.OutraOrdem }, p.Itens.Select(i => i.Ordem.Id));
            await Assert.ThrowsAsync<ConflictException>(() => s.CriarProgramacao(ids.Maquina, new(new DateOnly(2026, 9, 18), [])));
            await Assert.ThrowsAsync<DomainException>(() => s.AdicionarOrdem(p.Id, new(ids.Ordem, p.Versao)));
            await Assert.ThrowsAsync<DomainException>(() => s.Reordenar(p.Id, new([ids.Ordem, ids.Ordem], p.Versao)));
            await Assert.ThrowsAsync<DomainException>(() => s.Reordenar(p.Id, new([ids.Ordem], p.Versao)));
            await s.Reordenar(p.Id, new([ids.OutraOrdem, ids.Ordem], p.Versao));
            await Assert.ThrowsAsync<ConflictException>(() => s.Reordenar(p.Id, new([ids.Ordem, ids.OutraOrdem], p.Versao)));
        }
        await using var lido = banco.Open();
        var resultado = Assert.Single(await TestDatabase.Consulta(lido).Programacao(ids.Maquina, new DateOnly(2026, 9, 20)));
        Assert.Equal(new[] { ids.OutraOrdem, ids.Ordem }, resultado.Itens.Select(i => i.Ordem.Id));
        Assert.Equal(new[] { 1, 2 }, resultado.Itens.Select(i => i.Sequencia));
    }
    [Fact] public async Task SemanaNaoRecebeOFDeOutraMaquinaENaoGravaParcialmente()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        await using var db = banco.Open(); var s = TestDatabase.Programador(db);
        var outraMaquina = await s.CriarMaquina(new("M2", "Outra"));
        var outra = await s.CriarOrdem(new("OF-M2", ids.Produto, outraMaquina.Id, 10));
        await Assert.ThrowsAsync<DomainException>(() => s.CriarProgramacao(ids.Maquina, new(new DateOnly(2026, 9, 14), [ids.Ordem, outra.Id])));
        Assert.Equal(0, await db.Programacoes.CountAsync());
    }
}
