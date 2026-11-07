using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Maps the tenant directory table once, for every context that needs to read it.
/// </summary>
/// <remarks>
/// Shared on purpose. <see cref="TenantDirectoryDbContext"/> has to resolve a login slug into a tenant
/// before any tenant is known, and <see cref="OstagarDbContext"/> owns the same table through its
/// foreign keys. Two hand-written copies of one mapping drift, and the drift is invisible until a
/// production read fails on a column the migration never created.
/// </remarks>
public static class TenantEntityConfiguration
{
    public const string TableName = "tenants";

    public static void Apply(ModelBuilder builder) =>
        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable(TableName);

            entity.HasKey(tenant => tenant.Id);

            // The principal side of every tenant foreign key. Stored as uuid like the dependents, so
            // the relationship is type-compatible in both directions.
            entity.Property(tenant => tenant.Id).HasConversion(new TenantIdConverter());

            entity.Property(tenant => tenant.Slug).HasMaxLength(64);
            entity.Property(tenant => tenant.DisplayName).HasMaxLength(200);
            entity.Property(tenant => tenant.PreferredLanguage).HasMaxLength(16);
            entity.Property(tenant => tenant.FunctionalCurrencyCode).HasMaxLength(3);

            // The directory is not tenant-scoped: it is what makes a tenant id exist in the first
            // place. Slug is the only human-facing handle, so it is unique across the whole platform.
            entity.HasIndex(tenant => tenant.Slug)
                .IsUnique()
                .HasDatabaseName("UQ_tenants_slug");
        });
}
