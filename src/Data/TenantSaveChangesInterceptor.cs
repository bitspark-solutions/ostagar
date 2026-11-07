using Ostagar.Core;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Writes the resolved tenant onto every inserted row and refuses to mutate a row that belongs to
/// somebody else.
/// </summary>
/// <remarks>
/// The application-side half of isolation. Row-level security cannot be the only half, because the
/// database reads <c>app.tenant_id</c> to know who is asking, and only this interceptor can guarantee
/// that a new row's <c>tenant_id</c> is the tenant the session was opened for rather than a value that
/// came in into a request payload. It is also the only place that can set the shadow <c>TenantId</c> on
/// ASP.NET Identity's join tables, whose types the application does not own.
///
/// It throws rather than quietly re-tagging a row. A write that arrives with the wrong tenant is a bug
/// in the caller, and the legacy build's habit of swallowing those made an audit trail that was silently
/// wrong.
/// </remarks>
public sealed class TenantSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var tenantId = tenantContext.TenantId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var property = FindTenantProperty(entry);
            if (property is null)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                if (!Equals(property.CurrentValue, tenantId))
                {
                    property.CurrentValue = tenantId;
                }

                continue;
            }

            var ownedBy = property.OriginalValue is TenantId value ? value : TenantId.None;
            if (ownedBy != tenantId)
            {
                throw new InvalidOperationException(
                    $"Refusing to {entry.State} a {entry.Metadata.ShortName()} row owned by tenant {ownedBy} "
                    + $"while this operation is scoped to {tenantId}.");
            }

            property.IsModified = false;
        }
    }

    private static PropertyEntry? FindTenantProperty(EntityEntry entry) =>
        entry.Properties.FirstOrDefault(property => property.Metadata.Name == "TenantId");
}
