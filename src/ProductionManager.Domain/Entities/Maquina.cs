namespace ProductionManager.Domain.Entities;

public sealed class Maquina
{
    public int Id { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string Nome { get; private set; } = null!;
    public bool Ativa { get; private set; } = true;

    private Maquina() { }
    public Maquina(string codigo, string nome)
    {
        Codigo = Validacao.Codigo(codigo, "Código da máquina");
        Nome = Validacao.Texto(nome, "Nome da máquina");
    }

    public void Desativar() => Ativa = false;
    public void Ativar() => Ativa = true;
    public override string ToString() => $"{Codigo} - {Nome}";
}
