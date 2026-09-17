using System.Security.Claims;
using ProductionManager.Application.Interfaces;

namespace ProductionManager.Api;

/// <summary>Em desenvolvimento, um perfil explícito simula os papéis. Não é autenticação.</summary>
public sealed class HttpUsuarioAtual(IHttpContextAccessor accessor, IHostEnvironment environment) : IUsuarioAtual
{
    public string? UsuarioId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public Perfil? Perfil
    {
        get
        {
            var context = accessor.HttpContext;
            var valor = context?.User.Identity?.IsAuthenticated == true
                ? context.User.FindFirstValue(ClaimTypes.Role)
                : environment.IsDevelopment() ? context?.Request.Headers["X-Perfil"].ToString() : null;
            return valor switch { "Programador" => Application.Interfaces.Perfil.Programador,
                "Operador" => Application.Interfaces.Perfil.Operador, _ => null };
        }
    }
}
