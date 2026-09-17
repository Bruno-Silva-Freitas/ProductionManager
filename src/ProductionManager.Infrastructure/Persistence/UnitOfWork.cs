using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProductionManager.Application.Exceptions;
using ProductionManager.Application.Interfaces;

namespace ProductionManager.Infrastructure.Persistence;

public sealed class UnitOfWork(ProductionDbContext db) : IUnitOfWork
{
    public async Task Salvar(CancellationToken ct)
    {
        try
        {
            // SaveChanges usa uma transação para a raiz e seus filhos. Se a versão falhar,
            // nenhum apontamento/meta/alteração de sequência dessa operação fica gravado.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("Os dados foram alterados por outro cliente. Atualize a consulta.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            throw new ConflictException("A gravação conflita com dados existentes: código, OF, apontamento, programação ou máquina já em Produção.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 5 or 6 })
        {
            throw new ConflictException("Há outra gravação em andamento. Atualize os dados e tente novamente.", ex);
        }
    }
}
