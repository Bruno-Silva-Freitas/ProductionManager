using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProductionManager.Application.DTOs;
using ProductionManager.Application.Interfaces;

namespace ProductionManager.Tests.Integration;

public sealed class ApiTests
{
    private sealed class Factory(TestDatabase database, string environment = "Development") : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("ConnectionStrings:ProductionManager", database.ConnectionString);
        }
    }
    private static void Perfil(HttpClient client, string perfil)
    {
        client.DefaultRequestHeaders.Remove("X-Perfil");
        client.DefaultRequestHeaders.Add("X-Perfil", perfil);
    }
    private static async Task<T> Corpo<T>(HttpResponseMessage response, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var texto = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == esperado, $"Esperado {esperado}, recebido {response.StatusCode}: {texto}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact] public async Task FluxoCompletoApenasPorHttpIncluiCorrecaoPendenciasEFinalizacao()
    {
        using var banco = new TestDatabase(); using var factory = new Factory(banco); using var c = factory.CreateClient();
        Perfil(c, "Programador");
        var produto = await Corpo<ProdutoDto>(await c.PostAsJsonAsync("/api/produtos", new { pn = "00045872", nome = "Tampa" }), HttpStatusCode.Created);
        var maquina = await Corpo<MaquinaDto>(await c.PostAsJsonAsync("/api/maquinas", new { codigo = "INJ-04", nome = "Injetora 04" }), HttpStatusCode.Created);
        var o = await Corpo<OrdemDto>(await c.PostAsJsonAsync("/api/ordens", new { of = "000254879", produtoId = produto.Id, maquinaId = maquina.Id, quantidadePlanejada = 500 }), HttpStatusCode.Created);
        var p = await Corpo<ProgramacaoDto>(await c.PostAsJsonAsync($"/api/maquinas/{maquina.Id}/programacao", new { inicioSemana = "2026-09-16", ordemIds = new[] { o.Id } }), HttpStatusCode.Created);
        Assert.Equal(o.Id, Assert.Single(p.Itens).Ordem.Id);
        var busca = await Corpo<OrdemDto>(await c.GetAsync("/api/ordens/por-of/000254879"));
        Assert.Equal("00045872", busca.PN);
        Perfil(c, "Operador");
        o = await Corpo<OrdemDto>(await c.PostAsJsonAsync($"/api/ordens/{o.Id}/metas-hora",
            new { inicio = "2026-09-16T23:00:00-03:00", fim = "2026-09-17T00:00:00-03:00", metaPlanejada = 500, versao = o.Versao }));
        var metas = await Corpo<MetasHoraDto>(await c.GetAsync($"/api/ordens/{o.Id}/metas-hora"));
        var meta = Assert.Single(metas.Metas);
        Assert.Null(meta.Apontamento); Assert.Null(meta.Eficiencia); Assert.Equal(1, o.MetasPendentes);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/metas-hora/{meta.Id}/apontamento", new { quantidadeBoa = 480, refugo = 10, versao = o.Versao })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/ordens/{o.Id}/finalizar", new { versao = o.Versao })).StatusCode);
        o = await Corpo<OrdemDto>(await c.PatchAsJsonAsync($"/api/ordens/{o.Id}/status", new { status = "Producao", versao = o.Versao }));
        o = await Corpo<OrdemDto>(await c.PostAsJsonAsync($"/api/metas-hora/{meta.Id}/apontamento", new { quantidadeBoa = 525, refugo = 25, versao = o.Versao }));
        Assert.Equal(525, o.QuantidadeProduzida); Assert.Equal(25, o.Refugo); Assert.Equal(1.05m, o.Eficiencia);
        Assert.Equal("Producao", o.Status); Assert.Equal(0, o.MetasPendentes);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/metas-hora/{meta.Id}/apontamento", new { quantidadeBoa = 525, refugo = 25, versao = o.Versao })).StatusCode);
        o = await Corpo<OrdemDto>(await c.PutAsJsonAsync($"/api/metas-hora/{meta.Id}/apontamento", new { quantidadeBoa = 530, refugo = 20, versao = o.Versao }));
        var texto = await c.GetStringAsync($"/api/ordens/{o.Id}");
        Assert.Contains("\"eficiencia\":1.06", texto); Assert.DoesNotContain("%", texto);
        o = await Corpo<OrdemDto>(await c.PostAsJsonAsync($"/api/ordens/{o.Id}/finalizar", new { versao = o.Versao }));
        Assert.Equal("Finalizada", o.Status); Assert.NotNull(o.FinalizadaEm);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/metas-hora/{meta.Id}/apontamento", new { quantidadeBoa = 0, refugo = 0, versao = o.Versao })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PatchAsJsonAsync($"/api/ordens/{o.Id}/status", new { status = "SetUp", versao = o.Versao })).StatusCode);
        var final = await Corpo<OrdemDto>(await c.GetAsync($"/api/ordens/{o.Id}"));
        Assert.Equal(530, final.QuantidadeProduzida);
    }

    [Fact] public async Task HttpDistingue400404409E403()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        using var factory = new Factory(banco); using var c = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/produtos", new { pn = "P", nome = "P" })).StatusCode);
        Perfil(c, "Programador");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/produtos", new { pn = "", nome = "P" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/produtos", new { pn = "000PN-A", nome = "Outra" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/ordens/9999")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PatchAsJsonAsync($"/api/ordens/{ids.Ordem}/status", new { status = "Producao", versao = 1 })).StatusCode);
        Perfil(c, "Operador");
        Assert.Equal(HttpStatusCode.OK, (await c.PatchAsJsonAsync($"/api/ordens/{ids.Ordem}/status", new { status = "Producao", versao = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PatchAsJsonAsync($"/api/ordens/{ids.Ordem}/status", new { status = "Interrompida", versao = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PatchAsJsonAsync($"/api/ordens/{ids.OutraOrdem}/status", new { status = "Producao", versao = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PatchAsJsonAsync($"/api/ordens/{ids.Ordem}/status", new { status = "Finalizada", versao = 2 })).StatusCode);
        var malformed = await c.PatchAsync($"/api/ordens/{ids.Ordem}/status", new StringContent("{\"status\":999,\"versao\":2}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact] public async Task RequisicoesParalelasMesmaVersaoProduzemUmSucessoEUmConflito()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        using var factory = new Factory(banco); using var a = factory.CreateClient(); using var b = factory.CreateClient();
        Perfil(a, "Operador"); Perfil(b, "Operador");
        var respostas = await Task.WhenAll(
            a.PatchAsJsonAsync($"/api/ordens/{ids.Ordem}/status", new { status = "Producao", versao = 1 }),
            b.PatchAsJsonAsync($"/api/ordens/{ids.Ordem}/status", new { status = "Interrompida", versao = 1 }));
        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact] public async Task PerfilSimuladoNaoFuncionaEmProduction()
    {
        using var banco = new TestDatabase(); using var factory = new Factory(banco, "Production"); using var c = factory.CreateClient();
        Perfil(c, "Programador");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/produtos", new { pn = "P", nome = "P" })).StatusCode);
    }

    [Fact] public async Task CamposOmitidosNaoViraramApontamentoZeroImplicitamente()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        using var factory = new Factory(banco); using var c = factory.CreateClient();
        Perfil(c, "Operador");
        var response = await c.PostAsJsonAsync("/api/metas-hora/1/apontamento", new { versao = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"/api/ordens/{ids.Ordem}/finalizar", new { })).StatusCode);
    }

    [Fact] public async Task RequisicoesParalelasDeDuasOFsPreservamExclusividadeDaMaquina()
    {
        using var banco = new TestDatabase(); var ids = await banco.Seed();
        using var factory = new Factory(banco); using var a = factory.CreateClient(); using var b = factory.CreateClient();
        Perfil(a, "Operador"); Perfil(b, "Operador");
        var respostas = await Task.WhenAll(
            a.PatchAsJsonAsync($"/api/ordens/{ids.Ordem}/status", new { status = "Producao", versao = 1 }),
            b.PatchAsJsonAsync($"/api/ordens/{ids.OutraOrdem}/status", new { status = "Producao", versao = 1 }));
        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    private sealed class FalhaInesperadaRepository : IProdutoRepository
    {
        public Task<IReadOnlyList<ProductionManager.Domain.Entities.Produto>> Listar(CancellationToken ct) => throw new InvalidOperationException("SEGREDO-NAO-EXIBIR");
        public Task<ProductionManager.Domain.Entities.Produto?> Obter(int id, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> ExistePN(string pn, CancellationToken ct) => throw new NotImplementedException();
        public void Adicionar(ProductionManager.Domain.Entities.Produto produto) => throw new NotImplementedException();
    }
    [Fact] public async Task Erro500NaoVazaDetalhesInternos()
    {
        using var banco = new TestDatabase(); using var factory = new Factory(banco, "Production");
        using var alterada = factory.WithWebHostBuilder(builder => builder.ConfigureServices(s => s.AddScoped<IProdutoRepository, FalhaInesperadaRepository>()));
        using var c = alterada.CreateClient();
        var response = await c.GetAsync("/api/produtos");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var texto = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SEGREDO", texto);
        Assert.DoesNotContain("stack", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("traceId", texto);
    }
}
