using ProductionManager.Domain.Enums;
using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Domain.Entities;

/// <summary>Raiz do agregado: toda alteração de metas/apontamentos passa por esta entidade.</summary>
public sealed class OrdemProducao
{
    private readonly List<MetaHora> _metasHora = [];
    public int Id { get; private set; }
    public string OF { get; private set; } = null!;
    public int ProdutoId { get; private set; }
    public Produto Produto { get; private set; } = null!;
    public int MaquinaId { get; private set; }
    public Maquina Maquina { get; private set; } = null!;
    public int QuantidadePlanejada { get; private set; }
    public StatusOrdem Status { get; private set; } = StatusOrdem.SetUp;
    public DateTimeOffset CriadaEm { get; private set; }
    public DateTimeOffset? FinalizadaEm { get; private set; }
    public long Versao { get; private set; } = 1;
    public IReadOnlyCollection<MetaHora> MetasHora => _metasHora.AsReadOnly();
    public long QuantidadeProduzida => _metasHora.Sum(m => (long)(m.Apontamento?.QuantidadeBoa ?? 0));
    public long RefugoTotal => _metasHora.Sum(m => (long)(m.Apontamento?.Refugo ?? 0));
    public long MetaApontada => _metasHora.Where(m => m.Apontada).Sum(m => (long)m.MetaPlanejada);
    public int MetasPendentes => _metasHora.Count(m => !m.Apontada);
    public decimal Qualidade => QuantidadeProduzida + RefugoTotal == 0 ? 0
        : (decimal)QuantidadeProduzida / (QuantidadeProduzida + RefugoTotal);
    public decimal Eficiencia => MetaApontada == 0 ? 0 : (decimal)QuantidadeProduzida / MetaApontada;

    private OrdemProducao() { }
    public OrdemProducao(string of, Produto produto, Maquina maquina, int quantidadePlanejada, DateTimeOffset? agora = null)
    {
        OF = Validacao.Codigo(of, "OF");
        Produto = produto ?? throw new DomainException("Produto é obrigatório.");
        Maquina = maquina ?? throw new DomainException("Máquina é obrigatória.");
        if (!maquina.Ativa) throw new DomainException("A máquina está inativa.");
        Validacao.Positivo(quantidadePlanejada, "Quantidade planejada");
        ProdutoId = produto.Id;
        MaquinaId = maquina.Id;
        QuantidadePlanejada = quantidadePlanejada;
        CriadaEm = agora ?? DateTimeOffset.UtcNow;
    }

    public void AlterarStatusOperacional(StatusOrdem novoStatus)
    {
        VerificarAberta();
        if (novoStatus is not (StatusOrdem.SetUp or StatusOrdem.Producao or StatusOrdem.Interrompida))
            throw new DomainException("Selecione SetUp, Producao ou Interrompida. Finalização utiliza uma operação separada.");
        if (novoStatus == StatusOrdem.Producao && !Maquina.Ativa) throw new DomainException("A máquina está inativa.");
        Status = novoStatus;
        Versao++;
    }

    public MetaHora AdicionarMetaHora(DateTimeOffset inicio, DateTimeOffset fim, int metaPlanejada)
    {
        VerificarAberta();
        var meta = new MetaHora(Id, inicio, fim, metaPlanejada);
        if (_metasHora.Any(m => inicio < m.Fim && fim > m.Inicio))
            throw new DomainException("A Meta Hora se sobrepõe a outro período desta OF.");
        _metasHora.Add(meta);
        Versao++;
        return meta;
    }

    public void RegistrarApontamento(MetaHora meta, int quantidadeBoa, int refugo, DateTimeOffset? agora = null)
    {
        VerificarAberta();
        if (Status != StatusOrdem.Producao) throw new DomainException("A OF precisa estar em Produção para receber apontamentos.");
        VerificarMeta(meta);
        meta.Apontar(quantidadeBoa, refugo, agora ?? DateTimeOffset.UtcNow);
        Versao++;
    }

    // Corrigir é diferente de produzir: é permitido em SetUp/Pausa enquanto a OF estiver aberta.
    public void CorrigirApontamento(MetaHora meta, int quantidadeBoa, int refugo, DateTimeOffset? agora = null)
    {
        VerificarAberta();
        VerificarMeta(meta);
        meta.Corrigir(quantidadeBoa, refugo, agora ?? DateTimeOffset.UtcNow);
        Versao++;
    }

    public void Finalizar(DateTimeOffset? agora = null)
    {
        VerificarAberta();
        if (MetasPendentes > 0)
            throw new DomainException($"Existem {MetasPendentes} Metas Hora pendentes. Resolva os apontamentos antes de finalizar.");
        Status = StatusOrdem.Finalizada;
        FinalizadaEm = agora ?? DateTimeOffset.UtcNow;
        Versao++;
    }

    private void VerificarMeta(MetaHora meta)
    {
        if (meta is null || !_metasHora.Contains(meta)) throw new DomainException("A Meta Hora não pertence a esta OF.");
    }
    private void VerificarAberta()
    {
        if (Status == StatusOrdem.Finalizada) throw new DomainException("A OF foi finalizada e não pode ser alterada.");
    }
}
