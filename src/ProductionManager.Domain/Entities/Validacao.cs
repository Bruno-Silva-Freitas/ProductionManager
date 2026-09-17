using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Domain.Entities;

internal static class Validacao
{
    public static string Texto(string? valor, string nome) => string.IsNullOrWhiteSpace(valor)
        ? throw new DomainException($"{nome} é obrigatório.") : valor.Trim();

    public static string Codigo(string? valor, string nome) => Texto(valor, nome).ToUpperInvariant();

    public static void Positivo(int valor, string nome)
    {
        if (valor <= 0) throw new DomainException($"{nome} deve ser maior que zero.");
    }

    public static void Quantidades(int boas, int refugo)
    {
        if (boas < 0 || refugo < 0) throw new DomainException("Quantidade boa e refugo não podem ser negativos.");
    }
}
