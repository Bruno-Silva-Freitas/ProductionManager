namespace ProductionManager.Application.Interfaces;

public enum Perfil { Programador, Operador }

/// <summary>O adaptador de autenticação fornece o usuário; o domínio não conhece perfis.</summary>
public interface IUsuarioAtual
{
    string? UsuarioId { get; }
    Perfil? Perfil { get; }
}
