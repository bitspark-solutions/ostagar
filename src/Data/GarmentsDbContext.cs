using Garments.Core;
using Garments.Ledger;
using Garments.Modules.CashBook;
using Garments.Platform.Audit;
using Garments.Platform.Identity;
using Garments.Platform.Rbac;
using Garments.Platform.Tenancy;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Garments.Data;

/// <summary>
/// The schema the product runs on: the tenant directory, ASP.NET Identity, RBAC, the tenant's own
/// organization rows and the audit trail.
/// </summary>
/// <remarks>
/// Three mechanisms keep a row inside its tenant, because each one closes a hole the others leave.
/// <list type="number">
/// <item>Row-level security in Postgres filters reads and rejects writes below the application, so a
/// forgotten <c>WHERE</c> cannot leak. The legacy build had no tenancy column at all, so there was
/// nothing for it to protect.</item>
/// <item>A check constraint comparing <c>tenant_id</c> with <c>current_setting('app.tenant_id')</c>
/// rejects a mismatched write even for a principal that holds BYPASSRLS or owns the table.</item>
/// <item>Global query filters plus <see cref="TenantSaveChangesInterceptor"/> keep <c>tenant_id</c> out
/// of request payloads entirely, so a client cannot forge one.</item>
/// </list>
///
/// ASP.NET Identity's join and side tables (<c>user_roles</c>, <c>user_claims</c>, <c>user_logins</c>,
/// <c>user_tokens</c>, <c>role_claims</c>) carry a shadow <c>TenantId</c> rather than a CLR property:
/// <see cref="IdentityUser{TKey}"/> declares its navigations to the framework types, so substituting a
/// join entity of our own would fork Identity. A shadow property still gives those tables a real
/// <c>tenant_id</c> column, a composite index leading with it, the check constraint and row-level
/// security — which is what the isolation guarantee is actually built from.
///
/// Registered with <c>AddDbContext</c> and never <c>AddDbContextPool</c>: <c>app.tenant_id</c> is
/// session state on a connection, and a pooled context carries it — and the tracked rows that came with
/// it — into the next request. The legacy build pooled at 128.
/// </remarks>
public class GarmentsDbContext(DbContextOptions<GarmentsDbContext> options, ITenantContext tenantContext)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    /// <summary>
    /// Bangla-first ordering for the text a clerk actually searches. Declared per column because the dev
    /// and CI clusters are initialised with <c>--locale=C</c>, which is right for identifiers and wrong
    /// for ক, কা, খ, গ.
    /// </summary>
    public const string BanglaCollation = "bn-x-icu";

    public ITenantContext TenantContext => tenantContext;

    /// <summary>
    /// The tenant every query filter is bound to. An unresolved scope reads as
    /// <see cref="TenantId.None"/>, which matches no row: an unscoped read returns nothing rather than
    /// everything.
    /// </summary>
    public TenantId CurrentTenantId => tenantContext.HasTenant ? tenantContext.TenantId : TenantId.None;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<CompanyProfile> CompanyProfiles => Set<CompanyProfile>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<ResourceAction> ResourceActions => Set<ResourceAction>();

    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();

    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    public DbSet<ChartOfAccount> Accounts => Set<ChartOfAccount>();

    public DbSet<Journal> Journals => Set<Journal>();

    public DbSet<GlEntry> GlEntries => Set<GlEntry>();

    public DbSet<GlLine> GlLines => Set<GlLine>();

    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        TenantEntityConfiguration.Apply(builder);
        ConfigureCompanyProfiles(builder);
        ConfigureDepartments(builder);
        ConfigureResourceActions(builder);
        ConfigureRoleGrants(builder);
        ConfigureAuditLogs(builder);
        LedgerModelConfiguration.Apply(builder);
        CashBookModelConfiguration.Apply(builder);

        // Applied here rather than inside the ledger's own mapping because the filter has to close over
        // this context's tenant, and a static configuration class cannot reach it. Five calls instead of
        // a loop: a filter registered by reflection is one an entity can miss without a compile error,
        // which is the exact shape of the hole this project was rebuilt to close.
        TenantQueryFilter<ChartOfAccount>(builder);
        TenantQueryFilter<Journal>(builder);
        TenantQueryFilter<FiscalPeriod>(builder);
        TenantQueryFilter<GlEntry>(builder);
        TenantQueryFilter<GlLine>(builder);
        TenantQueryFilter<ExchangeRate>(builder);
        TenantQueryFilter<CashTransaction>(builder);

        SnakeCaseModelNames.Apply(builder);
    }

    private void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            ConfigureTenantTable(entity, "users");

            entity.Property(user => user.FullName).HasMaxLength(200);
            entity.Property(user => user.UserName).HasMaxLength(320);
            entity.Property(user => user.NormalizedUserName).HasMaxLength(320);
            entity.Property(user => user.Email).HasMaxLength(320);
            entity.Property(user => user.NormalizedEmail).HasMaxLength(320);

            // Identity's own indexes are global. A global uniqueness rule across tenants both collides
            // legitimately and tells tenant A that tenant B exists when it tries to add the same
            // address.
            //
            // Naming the problem is not fixing it: Identity's UNIQUE "UserNameIndex" is still on the
            // table unless it is redefined here, so two companies employing the same person's email
            // would be rejected on the second insert.
            entity.HasIndex(user => user.NormalizedUserName)
                .HasDatabaseName("IX_users_normalized_user_name")
                .IsUnique(false);

            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName("IX_users_normalized_email")
                .IsUnique(false);

            entity.HasIndex(user => new { user.TenantId, user.NormalizedUserName })
                .IsUnique()
                .HasDatabaseName("UQ_users_tenant_id_normalized_user_name");

            entity.HasIndex(user => new { user.TenantId, user.NormalizedEmail })
                .HasDatabaseName("IX_users_tenant_id_normalized_email");

            entity.HasQueryFilter(user => user.TenantId == CurrentTenantId);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            ConfigureTenantTable(entity, "roles");

            entity.Property(role => role.DisplayName).HasMaxLength(128);

            // ASP.NET Identity puts a UNIQUE index on NormalizedName across the whole table. In a
            // multi-tenant product that is wrong twice: the second company to create an "Owner" role
            // is rejected outright, and the collision itself confirms to tenant A that a role name
            // exists somewhere else in the system. Identity's index is replaced, not supplemented —
            // leaving it in place would keep the global constraint alive.
            entity.HasIndex(role => role.NormalizedName)
                .HasDatabaseName("IX_roles_normalized_name")
                .IsUnique(false);

            entity.HasIndex(role => new { role.TenantId, role.NormalizedName })
                .IsUnique()
                .HasDatabaseName("UQ_roles_tenant_id_normalized_name");

            entity.HasQueryFilter(role => role.TenantId == CurrentTenantId);
        });

        // No query filter on the five tables below: Identity's stores delete a user by cascading through
        // them, and a filter would make EF refuse the delete for rows it was not allowed to load.
        // Row-level security and the check constraint cover the same ground without breaking the store.
        builder.Entity<IdentityUserRole<Guid>>(entity =>
        {
            ConfigureShadowTenantTable(entity, "user_roles");
            entity.HasIndex(TenantOwnedTables.PropertyName, nameof(IdentityUserRole<Guid>.UserId))
                .HasDatabaseName("IX_user_roles_tenant_id_user_id");
        });

        builder.Entity<IdentityUserClaim<Guid>>(entity =>
        {
            ConfigureShadowTenantTable(entity, "user_claims");
            entity.HasIndex(TenantOwnedTables.PropertyName, nameof(IdentityUserClaim<Guid>.UserId))
                .HasDatabaseName("IX_user_claims_tenant_id_user_id");
        });

        builder.Entity<IdentityUserLogin<Guid>>(entity =>
        {
            ConfigureShadowTenantTable(entity, "user_logins");
            entity.HasIndex(TenantOwnedTables.PropertyName, nameof(IdentityUserLogin<Guid>.UserId))
                .HasDatabaseName("IX_user_logins_tenant_id_user_id");
        });

        builder.Entity<IdentityUserToken<Guid>>(entity =>
        {
            ConfigureShadowTenantTable(entity, "user_tokens");
            entity.HasIndex(TenantOwnedTables.PropertyName, nameof(IdentityUserToken<Guid>.UserId))
                .HasDatabaseName("IX_user_tokens_tenant_id_user_id");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(entity =>
        {
            ConfigureShadowTenantTable(entity, "role_claims");
            entity.HasIndex(TenantOwnedTables.PropertyName, nameof(IdentityRoleClaim<Guid>.RoleId))
                .HasDatabaseName("IX_role_claims_tenant_id_role_id");
        });
    }

    private void ConfigureCompanyProfiles(ModelBuilder builder) =>
        builder.Entity<CompanyProfile>(entity =>
        {
            ConfigureTenantTable(entity, "company_profiles");

            entity.Property(company => company.LegalName).HasMaxLength(300);
            entity.Property(company => company.LegalNameBangla).HasMaxLength(300);
            entity.Property(company => company.TradingName).HasMaxLength(300);
            entity.Property(company => company.TaxIdentificationNumber).HasMaxLength(32);
            entity.Property(company => company.AddressLine).HasMaxLength(500);
            entity.Property(company => company.ContactNumber).HasMaxLength(40);
            entity.Property(company => company.Email).HasMaxLength(320);

            entity.Property(company => company.LegalName).UseCollation(BanglaCollation);
            entity.Property(company => company.LegalNameBangla).UseCollation(BanglaCollation);
            entity.Property(company => company.TradingName).UseCollation(BanglaCollation);

            entity.HasIndex(company => company.TenantId)
                .IsUnique()
                .HasDatabaseName("UQ_company_profiles_tenant_id");

            entity.HasQueryFilter(company => company.TenantId == CurrentTenantId);
        });

    private void ConfigureDepartments(ModelBuilder builder) =>
        builder.Entity<Department>(entity =>
        {
            ConfigureTenantTable(entity, "departments");

            entity.Property(department => department.Code).HasMaxLength(32);
            entity.Property(department => department.Name).HasMaxLength(200);
            entity.Property(department => department.NameBangla).HasMaxLength(200);

            entity.Property(department => department.Name).UseCollation(BanglaCollation);
            entity.Property(department => department.NameBangla).UseCollation(BanglaCollation);

            entity.HasIndex(department => new { department.TenantId, department.Code })
                .IsUnique()
                .HasDatabaseName("UQ_departments_tenant_id_code");

            entity.HasIndex(department => new { department.TenantId, department.SortOrder })
                .HasDatabaseName("IX_departments_tenant_id_sort_order");

            entity.HasQueryFilter(department => department.TenantId == CurrentTenantId);
        });

    private static void ConfigureResourceActions(ModelBuilder builder) =>
        builder.Entity<ResourceAction>(entity =>
        {
            entity.ToTable("resource_actions");

            entity.Property(resourceAction => resourceAction.Resource).HasMaxLength(64);
            entity.Property(resourceAction => resourceAction.Action).HasMaxLength(32);
            entity.Property(resourceAction => resourceAction.Description).HasMaxLength(400);

            entity.HasIndex(resourceAction => new { resourceAction.Resource, resourceAction.Action })
                .IsUnique()
                .HasDatabaseName("UQ_resource_actions_resource_action");
        });

    private void ConfigureRoleGrants(ModelBuilder builder) =>
        builder.Entity<RoleGrant>(entity =>
        {
            ConfigureTenantTable(entity, "role_grants");

            entity.HasIndex(grant => new { grant.TenantId, grant.RoleId, grant.ResourceActionId })
                .IsUnique()
                .HasDatabaseName("UQ_role_grants_tenant_id_role_id_resource_action_id");

            entity.HasIndex(grant => new { grant.TenantId, grant.ResourceActionId })
                .HasDatabaseName("IX_role_grants_tenant_id_resource_action_id");

            entity.HasOne<ApplicationRole>()
                .WithMany()
                .HasForeignKey(grant => grant.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ResourceAction>()
                .WithMany()
                .HasForeignKey(grant => grant.ResourceActionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(grant => grant.TenantId == CurrentTenantId);
        });

    private void ConfigureAuditLogs(ModelBuilder builder) =>
        builder.Entity<AuditLogEntry>(entity =>
        {
            ConfigureTenantTable(entity, "audit_logs");

            entity.Property(audit => audit.Action).HasMaxLength(128);
            entity.Property(audit => audit.Resource).HasMaxLength(64);
            entity.Property(audit => audit.ResourceId).HasMaxLength(80);
            entity.Property(audit => audit.ActorName).HasMaxLength(320);
            entity.Property(audit => audit.CorrelationId).HasMaxLength(64);
            entity.Property(audit => audit.SourceAddress).HasMaxLength(64);

            // What changed, stored as jsonb so the trail survives a change to the table it describes.
            // Machine-readable payload, so it keeps the deterministic byte collation rather than Bangla.
            entity.Property(audit => audit.Detail).HasColumnType("jsonb");
            entity.Property(audit => audit.Outcome).HasConversion<string>().HasMaxLength(16);

            entity.HasIndex(audit => new { audit.TenantId, audit.OccurredAt })
                .HasDatabaseName("IX_audit_logs_tenant_id_occurred_at");

            entity.HasIndex(audit => new { audit.TenantId, audit.Resource, audit.ResourceId })
                .HasDatabaseName("IX_audit_logs_tenant_id_resource_resource_id");

            entity.HasQueryFilter(audit => audit.TenantId == CurrentTenantId);
        });

    /// <summary>
    /// Table name plus the tenancy column plus the check constraint, in one call, so a tenant-owned
    /// table cannot be declared without all three. The implementation is shared with
    /// <see cref="LedgerModelConfiguration"/> so the ledger cannot be given a weaker guarantee.
    /// </summary>
    private static void ConfigureTenantTable<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : class, ITenantScoped =>
        TenantTableConfiguration.ApplyTenantScope(entity, tableName);

    private static void ConfigureShadowTenantTable<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : class =>
        TenantTableConfiguration.ApplyShadowTenantScope(entity, tableName);

    /// <summary>
    /// Restricts every read of a tenant-owned entity to the session's tenant.
    /// </summary>
    /// <remarks>
    /// Row-level security already does this in the database, and the two are not redundant: RLS is what
    /// stops a raw SQL read or a component that never goes through EF, while the query filter is what
    /// makes an <c>Include</c>, a projection or a generated <c>count</c> come back with one company's
    /// rows. An entity with only the first is invisible to nobody — it just leaks through the ORM.
    /// </remarks>
    private void TenantQueryFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantScoped =>
        builder.Entity<TEntity>().HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
}
