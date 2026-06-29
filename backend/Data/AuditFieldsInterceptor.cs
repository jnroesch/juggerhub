using JuggerHub.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JuggerHub.Data;

/// <summary>
/// Populates <see cref="BaseEntity.CreatedDate"/> / <see cref="BaseEntity.ModifiedDate"/>
/// automatically on save (constitution Principle III).
/// </summary>
/// <remarks>
/// Sets <c>CreatedDate</c> on insert and <c>ModifiedDate</c> on insert/update,
/// in UTC. <c>ExecuteUpdateAsync</c>/<c>ExecuteDeleteAsync</c> bypass the change
/// tracker and therefore this interceptor — those paths must set
/// <c>ModifiedDate</c> explicitly.
/// </remarks>
public sealed class AuditFieldsInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private static void ApplyAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedDate = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.ModifiedDate = now;
            }
        }
    }
}
