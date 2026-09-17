using ProductionManager.Domain.Entities;
using ProductionManager.Domain.Enums;
using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Tests.Domain;

public sealed class DomainTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 16, 23, 0, 0, TimeSpan.FromHours(-3));
    private static OrdemProducao Ordem(int planejada = 1000) => new("000254879", new Produto("00045872", "Tampa"), new Maquina("INJ-04", "Injetora"), planejada);
    private static (OrdemProducao Ordem, MetaHora Meta) EmProducao(int meta = 500)
    {
        var o = Ordem();
        var m = o.AdicionarMetaHora(Inicio, Inicio.AddHours(1), meta);
        o.AlterarStatusOperacional(StatusOrdem.Producao);
        return (o, m);
    }

    [Fact] public void PreservaCodigosTextuais()
    {
        var o = Ordem();
        Assert.Equal("000254879", o.OF);
        Assert.Equal("00045872", o.Produto.PN);
        Assert.Equal("ABC-01", new Produto(" abc-01 ", " Peça ").PN);
        Assert.Equal("Peça", new Produto("abc", " Peça ").Nome);
        Assert.True(o.Maquina.Ativa);
        Assert.Equal(StatusOrdem.SetUp, o.Status);
        Assert.NotEqual(default, o.CriadaEm);
        Assert.Null(o.FinalizadaEm);
    }

    [Theory][InlineData("")][InlineData(" ")][InlineData(null)]
    public void RejeitaCodigosENomesVazios(string? texto)
    {
        Assert.Throws<DomainException>(() => new Produto(texto!, "Peça"));
        Assert.Throws<DomainException>(() => new Produto("PN", texto!));
        Assert.Throws<DomainException>(() => new Maquina(texto!, "Máquina"));
        Assert.Throws<DomainException>(() => new Maquina("M", texto!));
        Assert.Throws<DomainException>(() => new OrdemProducao(texto!, new Produto("P", "P"), new Maquina("M", "M"), 1));
    }

    [Fact] public void OrdemExigeProdutoEMaquina()
    {
        Assert.Throws<DomainException>(() => new OrdemProducao("1", null!, new Maquina("M", "M"), 1));
        Assert.Throws<DomainException>(() => new OrdemProducao("1", new Produto("P", "P"), null!, 1));
    }
    [Theory][InlineData(0)][InlineData(-1)]
    public void QuantidadePlanejadaPositiva(int valor) => Assert.Throws<DomainException>(() => Ordem(valor));

    [Fact] public void MaquinaInativaNaoRecebeNovaOrdemOuProducao()
    {
        var o = Ordem();
        o.Maquina.Desativar();
        Assert.Throws<DomainException>(() => new OrdemProducao("2", o.Produto, o.Maquina, 1));
        Assert.Throws<DomainException>(() => o.AlterarStatusOperacional(StatusOrdem.Producao));
        o.Maquina.Ativar();
        o.AlterarStatusOperacional(StatusOrdem.Producao);
    }
    [Fact] public void TransicoesOperacionaisSaoLivresEntreTresEstados()
    {
        var o = Ordem();
        foreach (var destino in new[] { StatusOrdem.Producao, StatusOrdem.Interrompida, StatusOrdem.Producao, StatusOrdem.SetUp, StatusOrdem.Interrompida })
        {
            o.AlterarStatusOperacional(destino);
            Assert.Equal(destino, o.Status);
        }
    }
    [Theory][InlineData(StatusOrdem.Finalizada)][InlineData((StatusOrdem)99)]
    public void RejeitaStatusForaDoFluxoOperacional(StatusOrdem status)
    {
        var o = Ordem();
        Assert.Throws<DomainException>(() => o.AlterarStatusOperacional(status));
        Assert.Equal(StatusOrdem.SetUp, o.Status);
    }
    [Fact] public void FinalizacaoExplicitaRegistraInstanteEImpedeTodasAsMutacoes()
    {
        var (o, m) = EmProducao();
        o.RegistrarApontamento(m, 100, 5);
        var fim = Inicio.AddHours(2);
        o.Finalizar(fim);
        Assert.Equal(StatusOrdem.Finalizada, o.Status);
        Assert.Equal(fim, o.FinalizadaEm);
        Assert.Throws<DomainException>(() => o.AlterarStatusOperacional(StatusOrdem.Producao));
        Assert.Throws<DomainException>(() => o.AdicionarMetaHora(fim, fim.AddHours(1), 500));
        Assert.Throws<DomainException>(() => o.RegistrarApontamento(m, 0, 0));
        Assert.Throws<DomainException>(() => o.CorrigirApontamento(m, 0, 0));
        Assert.Throws<DomainException>(() => o.Finalizar());
        Assert.Equal(100, o.QuantidadeProduzida);
    }
    [Fact] public void FinalizacaoSemMetasEValida()
    {
        var o = Ordem(); o.Finalizar(); Assert.Equal(StatusOrdem.Finalizada, o.Status);
    }
    [Fact] public void PendenciasBloqueiamFinalizacaoInclusiveFuturas()
    {
        var (o, m) = EmProducao();
        Assert.Throws<DomainException>(() => o.Finalizar());
        o.RegistrarApontamento(m, 0, 0);
        o.AdicionarMetaHora(Inicio.AddYears(1), Inicio.AddYears(1).AddHours(1), 1);
        Assert.Throws<DomainException>(() => o.Finalizar());
        Assert.Null(o.FinalizadaEm);
    }
    [Fact] public void AceitaMeiaNoiteEPreservaFuso()
    {
        var m = Ordem().AdicionarMetaHora(Inicio, Inicio.AddHours(1), 500);
        Assert.Equal(17, m.Fim.Day);
        Assert.Equal(0, m.Fim.Hour);
        Assert.Equal(TimeSpan.FromHours(-3), m.Inicio.Offset);
    }
    [Theory][InlineData(0)][InlineData(-1)]
    public void RejeitaIntervaloInvalido(int horas) => Assert.Throws<DomainException>(() => Ordem().AdicionarMetaHora(Inicio, Inicio.AddHours(horas), 500));
    [Fact] public void RejeitaInicioOmitido() => Assert.Throws<DomainException>(() => Ordem().AdicionarMetaHora(default, Inicio, 1));
    [Theory][InlineData(0)][InlineData(-500)]
    public void MetaExigePlanejadaPositiva(int meta) => Assert.Throws<DomainException>(() => Ordem().AdicionarMetaHora(Inicio, Inicio.AddHours(1), meta));

    [Theory][InlineData(0, 60)][InlineData(-30, 30)][InlineData(30, 90)][InlineData(-30, 90)]
    public void SobreposicoesSaoRejeitadas(int inicio, int fim)
    {
        var (o, _) = EmProducao();
        Assert.Throws<DomainException>(() => o.AdicionarMetaHora(Inicio.AddMinutes(inicio), Inicio.AddMinutes(fim), 1));
        Assert.Single(o.MetasHora);
    }
    [Fact] public void PeriodosAdjacentesSaoValidosESobreposicaoComOutroFusoNao()
    {
        var (o, _) = EmProducao();
        o.AdicionarMetaHora(Inicio.AddHours(1), Inicio.AddHours(2), 500);
        Assert.Throws<DomainException>(() => o.AdicionarMetaHora(Inicio.ToOffset(TimeSpan.Zero), Inicio.AddHours(1).ToOffset(TimeSpan.Zero), 500));
        Assert.Equal(2, o.MetasHora.Count);
    }
    [Theory][InlineData(StatusOrdem.SetUp)][InlineData(StatusOrdem.Interrompida)]
    public void ApontamentoExigeProducao(StatusOrdem status)
    {
        var (o, m) = EmProducao();
        o.AlterarStatusOperacional(status);
        Assert.Throws<DomainException>(() => o.RegistrarApontamento(m, 1, 0));
        Assert.False(m.Apontada);
    }
    [Fact] public void ZeroDistingueDeNaoApontado()
    {
        var (o, m) = EmProducao();
        Assert.Null(m.Apontamento);
        Assert.Equal(1, o.MetasPendentes);
        o.RegistrarApontamento(m, 0, 0);
        Assert.NotNull(m.Apontamento);
        Assert.Equal(0, o.MetasPendentes);
        Assert.Equal(500, o.MetaApontada);
        Assert.Equal(0m, o.Qualidade);
        Assert.Equal(0m, o.Eficiencia);
        o.Finalizar();
    }
    [Theory][InlineData(-1, 0)][InlineData(0, -1)][InlineData(-1, -1)]
    public void NegativosNaoAlteramResultado(int boas, int refugo)
    {
        var (o, m) = EmProducao();
        Assert.Throws<DomainException>(() => o.RegistrarApontamento(m, boas, refugo));
        Assert.Null(m.Apontamento);
        o.RegistrarApontamento(m, 50, 5);
        Assert.Throws<DomainException>(() => o.CorrigirApontamento(m, boas, refugo));
        Assert.Equal(50, o.QuantidadeProduzida);
        Assert.Equal(5, o.RefugoTotal);
    }
    [Fact] public void DuplicacaoNaoSomaECorrecaoMantemRegistro()
    {
        var (o, m) = EmProducao();
        o.RegistrarApontamento(m, 500, 10, Inicio);
        var apontamento = m.Apontamento;
        Assert.Throws<DomainException>(() => o.RegistrarApontamento(m, 500, 10));
        o.AlterarStatusOperacional(StatusOrdem.Interrompida);
        o.CorrigirApontamento(m, 450, 20, Inicio.AddMinutes(5));
        Assert.Same(apontamento, m.Apontamento);
        Assert.Equal(Inicio, m.Apontamento!.RegistradoEm);
        Assert.Equal(Inicio.AddMinutes(5), m.Apontamento.AtualizadoEm);
        Assert.Equal(450, o.QuantidadeProduzida);
        Assert.Equal(20, o.RefugoTotal);
    }
    [Fact] public void CorrecaoSemApontamentoNaoCriaRegistro()
    {
        var (o, m) = EmProducao();
        Assert.Throws<DomainException>(() => o.CorrigirApontamento(m, 0, 0));
        Assert.Null(m.Apontamento);
    }
    [Fact] public void MetaDeOutraOFNaoPodeSerApontadaOuCorrigida()
    {
        var (o, _) = EmProducao();
        var (_, estrangeira) = EmProducao();
        Assert.Throws<DomainException>(() => o.RegistrarApontamento(estrangeira, 1, 0));
        Assert.Throws<DomainException>(() => o.CorrigirApontamento(estrangeira, 1, 0));
    }
    [Fact] public void IndicadoresSomamBoasERefugoEIgnoramMetasSemApontamento()
    {
        var (o, m1) = EmProducao();
        var m2 = o.AdicionarMetaHora(Inicio.AddHours(1), Inicio.AddHours(2), 500);
        o.AdicionarMetaHora(Inicio.AddHours(2), Inicio.AddHours(3), 500);
        o.RegistrarApontamento(m1, 480, 10);
        o.RegistrarApontamento(m2, 510, 5);
        Assert.Equal(990, o.QuantidadeProduzida);
        Assert.Equal(15, o.RefugoTotal);
        Assert.Equal(1000, o.MetaApontada);
        Assert.Equal(990m / 1005m, o.Qualidade);
        Assert.Equal(0.99m, o.Eficiencia);
        Assert.Equal(1, o.MetasPendentes);
    }
    [Theory][InlineData(450, 0.9)][InlineData(500, 1.0)][InlineData(525, 1.05)]
    public void EficienciaPodeSerMenorIgualOuMaiorQue100(int boas, double esperado)
    {
        var (o, m) = EmProducao();
        o.RegistrarApontamento(m, boas, 0);
        Assert.Equal((decimal)esperado, o.Eficiencia);
        Assert.Equal((decimal)esperado, m.Eficiencia);
        Assert.Equal(1m, o.Qualidade);
    }
    [Theory][InlineData(1000)][InlineData(1500)]
    public void AtingirOuUltrapassarPlanejadoNaoFinaliza(int quantidade)
    {
        var (o, m) = EmProducao(); o.RegistrarApontamento(m, quantidade, 0);
        Assert.Equal(StatusOrdem.Producao, o.Status); Assert.Null(o.FinalizadaEm);
    }
    [Fact] public void TotaisNaoEstouramInteiro32Bits()
    {
        var (o, m) = EmProducao(int.MaxValue);
        var m2 = o.AdicionarMetaHora(Inicio.AddHours(1), Inicio.AddHours(2), int.MaxValue);
        o.RegistrarApontamento(m, int.MaxValue, int.MaxValue);
        o.RegistrarApontamento(m2, int.MaxValue, int.MaxValue);
        Assert.Equal(2L * int.MaxValue, o.QuantidadeProduzida);
        Assert.Equal(.5m, o.Qualidade);
        Assert.Equal(.5m, m.Qualidade);
    }
    [Fact] public void IndicadoresSemProducaoSaoZero()
    {
        var o = Ordem(); Assert.Equal(0m, o.Qualidade); Assert.Equal(0m, o.Eficiencia);
    }
    [Fact] public void ProgramacaoExigeMesmaMaquinaENaoDuplicaOF()
    {
        var o = Ordem();
        var p = new ProgramacaoSemanal(o.Maquina, new DateOnly(2026, 9, 16));
        Assert.Equal(new DateOnly(2026, 9, 14), p.InicioSemana);
        p.AdicionarOrdem(o);
        Assert.Equal(1, Assert.Single(p.Itens).Sequencia);
        Assert.Throws<DomainException>(() => p.AdicionarOrdem(o));
        Assert.Throws<DomainException>(() => p.AdicionarOrdem(new OrdemProducao("2", o.Produto, new Maquina("M2", "M2"), 1)));
    }
    [Fact] public void ProgramacaoNaoRecebeOFFinalizada()
    {
        var o = Ordem(); o.Finalizar();
        Assert.Throws<DomainException>(() => new ProgramacaoSemanal(o.Maquina, new DateOnly(2026, 9, 14)).AdicionarOrdem(o));
    }
    [Fact] public void EntidadesNaoExpoemSettersPublicosNemMutacaoDiretaDosFilhos()
    {
        foreach (var tipo in new[] { typeof(Produto), typeof(Maquina), typeof(OrdemProducao), typeof(MetaHora), typeof(ApontamentoMetaHora), typeof(ProgramacaoSemanal), typeof(ItemProgramacao) })
            Assert.DoesNotContain(tipo.GetProperties(), p => p.SetMethod?.IsPublic == true);
        Assert.DoesNotContain(typeof(MetaHora).GetMethods(), m => m.DeclaringType == typeof(MetaHora) && !m.IsSpecialName);
        Assert.DoesNotContain(typeof(ApontamentoMetaHora).GetMethods(), m => m.DeclaringType == typeof(ApontamentoMetaHora) && !m.IsSpecialName);
        var o = Ordem();
        Assert.Throws<NotSupportedException>(() => ((ICollection<MetaHora>)o.MetasHora).Clear());
    }
}
