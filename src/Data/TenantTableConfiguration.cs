using Ostagar.Core;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ostagar.Data;

/// <summary>
/// Gives a table the three things that make its tenancy real, in one place.
/// </summary>
/// <remarks>
/// Row-level security alone is not enough: a policy decides which rows a session sees, but nothing
/// stops a write putting a row under a different tenant than the session. The check constraint closes
/// that, and the converter keeps <c>tenant_id</c> type-compatible with the directory's primary key so
/// the foreign key can exist at all.
///
/// Extracted from <see cref="OstagarDbContext"/> when the ledger was mapped, because the same three
/// decisions have to hold for every tenant-owned table and two copies of them would drift into a
/// schema where one table is silently weaker than the rest.
/// </remarks>
internal static class TenantTableConfiguration
{
    /// <summary>For an entity declaring <c>TenantId</c> itself — every domain entity.</summary>
    internal static void ApplyTenantScope<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : class, ITenantScoped
    {
        ApplyCheckConstraint(entity, tableName);

        entity.Property(tenantScoped => tenantScoped.TenantId)
            .IsRequired()
            .HasConversion(new TenantIdConverter());

        // Restrict rather than the conventional cascade. A tenant row is deleted only by an explicit
        // offboarding that has already decided what the ledger becomes, and a cascade from a mistyped
        // delete would take the general ledger with it silently and completely.
        entity.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(tenantScoped => tenantScoped.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// For a table that carries tenancy but cannot declare it — the Identity join tables are owned by
    /// the framework, so their <c>tenant_id</c> has to be a shadow property.
    /// </summary>
    internal static void ApplyShadowTenantScope<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : class
    {
        ApplyCheckConstraint(entity, tableName);

        entity.Property<TenantId>(TenantOwnedTables.PropertyName)
            .IsRequired()
            .HasConversion(new TenantIdConverter());
    }

    private static void ApplyCheckConstraint<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : class =>
        entity.ToTable(tableName, table => table.HasCheckConstraint(
            TenantOwnedTables.CheckName(tableName),
            $"{TenantOwnedTables.PropertyColumnName} IS NOT NULL AND {TenantOwnedTables.TenantScopePredicate()}"));
}
