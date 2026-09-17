using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Domain.Entities;

public sealed class ProgramacaoSemanal
{
    private readonly List<ItemProgramacao> _itens = [];
    public int Id { get; private set; }
    public int MaquinaId { get; private set; }
    public Maquina Maquina { get; private set; } = null!;
    public DateOnly InicioSemana { get; private set; }
    public long Versao { get; private set; } = 1;
    public IReadOnlyCollection<ItemProgramacao> Itens => _itens.AsReadOnly();

    private ProgramacaoSemanal() { }
    public ProgramacaoSemanal(Maquina maquina, DateOnly inicioSemana)
    {
        Maquina = maquina ?? throw new DomainException("Máquina é obrigatória.");
        if (!maquina.Ativa) throw new DomainException("A máquina está inativa.");
        if (inicioSemana == default) throw new DomainException("Informe uma data para a programação.");
        MaquinaId = maquina.Id;
        InicioSemana = inicioSemana.AddDays(-(((int)inicioSemana.DayOfWeek + 6) % 7));
    }

    public void AdicionarOrdem(OrdemProducao ordem)
    {
        if (ordem is null) throw new DomainException("OF é obrigatória.");
        if (!ReferenceEquals(ordem.Maquina, Maquina) && (MaquinaId == 0 || ordem.MaquinaId != MaquinaId))
            throw new DomainException("A OF pertence a outra máquina.");
        if (_itens.Any(i => i.OrdemProducao.OF == ordem.OF)) throw new DomainException("A OF já está nesta programação.");
        if (ordem.Status == Enums.StatusOrdem.Finalizada) throw new DomainException("Não é possível programar uma OF finalizada.");
        _itens.Add(new ItemProgramacao(ordem, _itens.Count + 1));
        Versao++;
    }

    public void Reordenar(IReadOnlyList<int> ordensEmSequencia)
    {
        if (ordensEmSequencia is null || ordensEmSequencia.Count != _itens.Count
            || ordensEmSequencia.Distinct().Count() != _itens.Count
            || ordensEmSequencia.Any(id => !_itens.Any(i => i.OrdemProducaoId == id)))
            throw new DomainException("Informe todas as OFs da programação exatamente uma vez, na sequência desejada.");
        for (var i = 0; i < ordensEmSequencia.Count; i++)
            _itens.Single(item => item.OrdemProducaoId == ordensEmSequencia[i]).DefinirSequencia(i + 1);
        Versao++;
    }
}
