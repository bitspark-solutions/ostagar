using Ostagar.Core;

namespace Ostagar.Platform.Tenancy;

/// <summary>
/// Request-scoped holder for the resolved tenant. Scoped, never singleton: a shared instance across
/// requests is exactly how a tenant identifier leaks from one customer's request into another's.
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    private TenantId? _tenantId;

    public TenantId TenantId
    {
        get
        {
            if (_tenantId is { } resolved)
            {
                return resolved;
            }

            // A system scope has no tenant by definition, and query filters still evaluate this when
            // EF builds the expression tree for an unrelated table. Resolving to None means "matches
            // no row", which is the correct answer and is enforced again by row-level security — the
            // alternative is throwing here, which turns a global-table write into a startup crash.
            if (IsSystemScope)
            {
                return TenantId.None;
            }

            throw new InvalidOperationException(
                "No tenant has been resolved for this operation. Every query and every write must be "
                + "tenant-scoped, so an unscoped operation is a defect rather than an empty result.");
        }
    }

    public bool HasTenant => _tenantId is not null;

    /// <summary>Set once per request by the tenancy middleware; a second different value is a bug.</summary>
    public void Set(TenantId tenantId)
    {
        if (tenantId.IsEmpty)
        {
            throw new ArgumentException("A request cannot be scoped to an empty tenant.", nameof(tenantId));
        }

        if (_tenantId is not null && _tenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"Tenant is already resolved to {_tenantId} and cannot be reassigned to {tenantId} within one scope.");
        }

        _tenantId = tenantId;
    }

    /// <summary>
    /// Opens the narrow escape hatch that startup and maintenance need.
    /// </summary>
    /// <remarks>
    /// Some tables are global on purpose — the permission catalogue has no <c>tenant_id</c> because it
    /// describes the product, not a company — and seeding one of them is not a tenant operation, so
    /// the default refusal has to be liftable. Lifting it is explicit and greppable rather than the
    /// interceptor quietly allowing a null tenant, which is the shape that turns into an accidental
    /// cross-tenant read the first time somebody adds a tenant table to the same code path.
    ///
    /// This is not a bypass. Row-level security still applies to every tenant-owned table, so a
    /// system scope that queries one gets zero rows, not every row.
    /// </remarks>
    public void EnterSystemScope()
    {
        if (_tenantId is not null)
        {
            throw new InvalidOperationException("A scope that already resolved a tenant cannot also claim system scope.");
        }

        IsSystemScope = true;
    }

    public bool IsSystemScope { get; private set; }
}
