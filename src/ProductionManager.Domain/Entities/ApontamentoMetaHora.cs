namespace ProductionManager.Domain.Entities;

public sealed class ApontamentoMetaHora
{
    public int Id { get; private set; }
    public int MetaHoraId { get; private set; }
    public int QuantidadeBoa { get; private set; }
    public int Refugo { get; private set; }
    public DateTimeOffset RegistradoEm { get; private set; }
    public DateTimeOffset? AtualizadoEm { get; private set; }

    private ApontamentoMetaHora() { }
    internal ApontamentoMetaHora(int metaHoraId, int quantidadeBoa, int refugo, DateTimeOffset agora)
    {
        Validacao.Quantidades(quantidadeBoa, refugo);
        MetaHoraId = metaHoraId;
        QuantidadeBoa = quantidadeBoa;
        Refugo = refugo;
        RegistradoEm = agora;
    }

    internal void Corrigir(int quantidadeBoa, int refugo, DateTimeOffset agora)
    {
        Validacao.Quantidades(quantidadeBoa, refugo);
        QuantidadeBoa = quantidadeBoa;
        Refugo = refugo;
        AtualizadoEm = agora;
    }
}
