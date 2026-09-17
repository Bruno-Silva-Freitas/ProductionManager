using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ProductionManager.Api;
using ProductionManager.Api.Contracts;
using ProductionManager.Application.Interfaces;
using ProductionManager.Infrastructure;
using ProductionManager.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
// Logs portáveis: não dependem de permissões no Event Log do Windows.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiErrors>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    // Campos omitidos não viram silenciosamente zero, especialmente em apontamentos.
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
});
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, HttpUsuarioAtual>();
var connectionString = builder.Configuration.GetConnectionString("ProductionManager");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var data = Path.Combine(builder.Environment.ContentRootPath, "data");
    Directory.CreateDirectory(data);
    connectionString = $"Data Source={Path.Combine(data, "productionmanager.db")};Foreign Keys=True";
}
builder.Services.AddProductionManager(connectionString);
var app = builder.Build();
app.UseExceptionHandler();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
    await db.Database.EnsureCreatedAsync();
}
app.MapProductionManager();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program;
