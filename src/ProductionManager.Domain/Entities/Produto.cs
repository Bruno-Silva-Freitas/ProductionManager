namespace ProductionManager.Domain.Entities;

public sealed class Produto
{
    public int Id { get; private set; }
    public string PN { get; private set; } = null!;
    public string Nome { get; private set; } = null!;

    private Produto() { }
    public Produto(string pn, string nome)
    {
        PN = Validacao.Codigo(pn, "PN");
        Nome = Validacao.Texto(nome, "Nome do produto");
    }

    public override string ToString() => $"{PN} - {Nome}";
}
