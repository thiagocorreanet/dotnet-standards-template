using Microsoft.EntityFrameworkCore;

namespace Shared.Data.Extensions;

public static class DbContextTransactionExtensions
{
    /// <summary>Participa da fronteira do comando. Consumidor autônomo usa transação local sem replay de tracker.</summary>
    public static async Task<TResult> ExecuteInTransactionAsync<TResult>(this DbContext db,
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
            return await operation(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
