namespace ProductionManager.Domain.Entities;

public sealed class ItemProgramacao
{
    public int Id { get; private set; }
    public int ProgramacaoSemanalId { get; private set; }
    public int OrdemProducaoId { get; private set; }
    public OrdemProducao OrdemProducao { get; private set; } = null!;
    public int Sequencia { get; private set; }
    private ItemProgramacao() { }
    internal ItemProgramacao(OrdemProducao ordem, int sequencia)
    {
        OrdemProducao = ordem;
        OrdemProducaoId = ordem.Id;
        Sequencia = sequencia;
    }
    internal void DefinirSequencia(int sequencia) => Sequencia = sequencia;
}
