using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Domain.Entities;

public sealed class MetaHora
{
    public int Id { get; private set; }
    public int OrdemProducaoId { get; private set; }
    public DateTimeOffset Inicio { get; private set; }
    public DateTimeOffset Fim { get; private set; }
    public int MetaPlanejada { get; private set; }
    public ApontamentoMetaHora? Apontamento { get; private set; }
    public bool Apontada => Apontamento is not null;
    public decimal Eficiencia => (decimal)(Apontamento?.QuantidadeBoa ?? 0) / MetaPlanejada;
    public decimal Qualidade
    {
        get
        {
            long total = (long)(Apontamento?.QuantidadeBoa ?? 0) + (Apontamento?.Refugo ?? 0);
            return total == 0 ? 0 : (decimal)Apontamento!.QuantidadeBoa / total;
        }
    }

    private MetaHora() { }
    internal MetaHora(int ordemProducaoId, DateTimeOffset inicio, DateTimeOffset fim, int metaPlanejada)
    {
        if (inicio == default || fim <= inicio) throw new DomainException("Informe início válido e fim posterior ao início da Meta Hora.");
        Validacao.Positivo(metaPlanejada, "Meta planejada");
        OrdemProducaoId = ordemProducaoId;
        Inicio = inicio;
        Fim = fim;
        MetaPlanejada = metaPlanejada;
    }

    internal void Apontar(int boas, int refugo, DateTimeOffset agora)
    {
        if (Apontada) throw new DomainException("Esta Meta Hora já possui apontamento. Utilize a correção explícita.");
        Apontamento = new ApontamentoMetaHora(Id, boas, refugo, agora);
    }

    internal void Corrigir(int boas, int refugo, DateTimeOffset agora)
    {
        if (Apontamento is null) throw new DomainException("Não existe apontamento para corrigir.");
        Apontamento.Corrigir(boas, refugo, agora);
    }
}
