using ProductionManager.Domain.Enums;

namespace ProductionManager.Application.Commands;

public sealed record CriarProduto(string PN, string Nome);
public sealed record CriarMaquina(string Codigo, string Nome);
public sealed record CriarOrdem(string OF, int ProdutoId, int MaquinaId, int QuantidadePlanejada);
public sealed record AlterarStatus(StatusOrdem Status, long Versao);
public sealed record FinalizarOrdem(long Versao);
public sealed record CriarMetaHora(DateTimeOffset Inicio, DateTimeOffset Fim, int MetaPlanejada, long Versao);
public sealed record ApontarMetaHora(int QuantidadeBoa, int Refugo, long Versao);
public sealed record CriarProgramacao(DateOnly InicioSemana, int[] OrdemIds);
public sealed record AdicionarOrdemProgramacao(int OrdemId, long Versao);
public sealed record ReordenarProgramacao(int[] OrdemIds, long Versao);
