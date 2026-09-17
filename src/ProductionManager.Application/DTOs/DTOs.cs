using ProductionManager.Domain.Entities;

namespace ProductionManager.Application.DTOs;

public sealed record ProdutoDto(int Id, string PN, string Nome);
public sealed record MaquinaDto(int Id, string Codigo, string Nome, bool Ativa);
public sealed record ApontamentoDto(int Id, int MetaHoraId, int QuantidadeBoa, int Refugo,
    DateTimeOffset RegistradoEm, DateTimeOffset? AtualizadoEm);
public sealed record MetaHoraDto(int Id, int OrdemProducaoId, DateTimeOffset Inicio, DateTimeOffset Fim,
    int MetaPlanejada, ApontamentoDto? Apontamento, decimal? Eficiencia, decimal? Qualidade);
public sealed record OrdemDto(int Id, string OF, string PN, string Produto, int MaquinaId,
    string MaquinaCodigo, string MaquinaNome, int QuantidadePlanejada, long QuantidadeProduzida,
    long Refugo, long MetaApontada, decimal Qualidade, decimal Eficiencia, string Status,
    int MetasPendentes, DateTimeOffset CriadaEm, DateTimeOffset? FinalizadaEm, long Versao);
public sealed record MetasHoraDto(int OrdemId, long Versao, IReadOnlyList<MetaHoraDto> Metas);
public sealed record ItemProgramacaoDto(int Id, int Sequencia, OrdemDto Ordem);
public sealed record ProgramacaoDto(int Id, int MaquinaId, string MaquinaCodigo, DateOnly InicioSemana,
    long Versao, IReadOnlyList<ItemProgramacaoDto> Itens);

public static class Mapeamento
{
    public static ProdutoDto Dto(this Produto p) => new(p.Id, p.PN, p.Nome);
    public static MaquinaDto Dto(this Maquina m) => new(m.Id, m.Codigo, m.Nome, m.Ativa);
    public static OrdemDto Dto(this OrdemProducao o) => new(o.Id, o.OF, o.Produto.PN, o.Produto.Nome,
        o.MaquinaId, o.Maquina.Codigo, o.Maquina.Nome, o.QuantidadePlanejada, o.QuantidadeProduzida,
        o.RefugoTotal, o.MetaApontada, o.Qualidade, o.Eficiencia, o.Status.ToString(), o.MetasPendentes,
        o.CriadaEm, o.FinalizadaEm, o.Versao);
    public static MetaHoraDto Dto(this MetaHora m) => new(m.Id, m.OrdemProducaoId, m.Inicio, m.Fim,
        m.MetaPlanejada, m.Apontamento is { } a ? new(a.Id, a.MetaHoraId, a.QuantidadeBoa, a.Refugo,
        a.RegistradoEm, a.AtualizadoEm) : null, m.Apontada ? m.Eficiencia : null, m.Apontada ? m.Qualidade : null);
    public static ProgramacaoDto Dto(this ProgramacaoSemanal p) => new(p.Id, p.MaquinaId, p.Maquina.Codigo,
        p.InicioSemana, p.Versao, p.Itens.OrderBy(i => i.Sequencia).Select(i => new ItemProgramacaoDto(i.Id, i.Sequencia, i.OrdemProducao.Dto())).ToList());
}
