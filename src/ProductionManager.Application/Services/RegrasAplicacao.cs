using ProductionManager.Application.Exceptions;
using ProductionManager.Application.Interfaces;
using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Application.Services;

internal static class RegrasAplicacao
{
    public static void ExigirPerfil(IUsuarioAtual usuario, Perfil perfil)
    {
        if (usuario.Perfil != perfil) throw new ForbiddenException($"Esta operação exige perfil {perfil}.");
    }
    public static void ExigirVersao(long atual, long enviada)
    {
        if (enviada <= 0) throw new DomainException("Informe a versão obtida na última consulta.");
        if (atual != enviada) throw new ConflictException("Os dados foram alterados por outro cliente. Atualize a consulta e tente novamente.");
    }
    public static void ExigirId(int id, string campo)
    {
        if (id <= 0) throw new DomainException($"{campo} é obrigatório.");
    }
}
