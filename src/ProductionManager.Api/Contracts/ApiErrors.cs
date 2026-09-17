using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProductionManager.Application.Exceptions;
using ProductionManager.Domain.Exceptions;

namespace ProductionManager.Api.Contracts;

public sealed class ApiErrors(ILogger<ApiErrors> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (400, "Dados ou operação inválidos"),
            BadHttpRequestException => (400, "Requisição inválida"),
            NotFoundException => (404, "Registro não encontrado"),
            ConflictException => (409, "Conflito de dados"),
            ForbiddenException => (403, "Operação não permitida"),
            _ => (500, "Erro interno")
        };
        if (status == 500) logger.LogError(exception, "Erro inesperado. Requisição {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == 500 ? "Não foi possível concluir a operação." : exception.Message,
            Extensions = { ["traceId"] = context.TraceIdentifier }
        }, cancellationToken: ct);
        return true;
    }
}
