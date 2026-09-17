using ProductionManager.Application.Commands;
using ProductionManager.Application.Services;

namespace ProductionManager.Api;

public static class Endpoints
{
    public static void MapProductionManager(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        api.MapGet("/produtos", (ConsultaService s, CancellationToken ct) => s.Produtos(ct));
        api.MapPost("/produtos", async (CriarProduto c, ProgramadorService s, CancellationToken ct) =>
        {
            var dto = await s.CriarProduto(c, ct);
            return Results.Created("/api/produtos", dto);
        });
        api.MapGet("/maquinas", (ConsultaService s, CancellationToken ct) => s.Maquinas(ct));
        api.MapPost("/maquinas", async (CriarMaquina c, ProgramadorService s, CancellationToken ct) =>
        {
            var dto = await s.CriarMaquina(c, ct);
            return Results.Created("/api/maquinas", dto);
        });
        api.MapPost("/ordens", async (CriarOrdem c, ProgramadorService s, CancellationToken ct) =>
        {
            var dto = await s.CriarOrdem(c, ct);
            return Results.Created($"/api/ordens/{dto.Id}", dto);
        });
        api.MapGet("/ordens/{id:int}", (int id, ConsultaService s, CancellationToken ct) => s.Ordem(id, ct));
        api.MapGet("/ordens/por-of/{of}", (string of, ConsultaService s, CancellationToken ct) => s.PorOF(of, ct));
        api.MapPatch("/ordens/{id:int}/status", (int id, AlterarStatus c, OperadorService s, CancellationToken ct) => s.AlterarStatus(id, c, ct));
        api.MapPost("/ordens/{id:int}/finalizar", (int id, FinalizarOrdem c, OperadorService s, CancellationToken ct) => s.Finalizar(id, c, ct));
        api.MapGet("/ordens/{id:int}/metas-hora", (int id, ConsultaService s, CancellationToken ct) => s.Metas(id, ct));
        api.MapPost("/ordens/{id:int}/metas-hora", (int id, CriarMetaHora c, OperadorService s, CancellationToken ct) => s.AdicionarMeta(id, c, ct));
        api.MapPost("/metas-hora/{id:int}/apontamento", (int id, ApontarMetaHora c, OperadorService s, CancellationToken ct) => s.Apontar(id, c, ct));
        api.MapPut("/metas-hora/{id:int}/apontamento", (int id, ApontarMetaHora c, OperadorService s, CancellationToken ct) => s.Corrigir(id, c, ct));
        api.MapGet("/maquinas/{id:int}/programacao", (int id, DateOnly? semana, ConsultaService s, CancellationToken ct) => s.Programacao(id, semana, ct));
        api.MapPost("/maquinas/{id:int}/programacao", async (int id, CriarProgramacao c, ProgramadorService s, CancellationToken ct) =>
        {
            var dto = await s.CriarProgramacao(id, c, ct);
            return Results.Created($"/api/maquinas/{id}/programacao?semana={dto.InicioSemana:yyyy-MM-dd}", dto);
        });
        api.MapPost("/programacoes/{id:int}/itens", (int id, AdicionarOrdemProgramacao c, ProgramadorService s, CancellationToken ct) => s.AdicionarOrdem(id, c, ct));
        api.MapPut("/programacoes/{id:int}/sequencia", (int id, ReordenarProgramacao c, ProgramadorService s, CancellationToken ct) => s.Reordenar(id, c, ct));
    }
}
