using Ostagar.Core;

namespace Ostagar.Platform.Tenancy;

/// <summary>
/// One tenant: a single garment factory or trading company using the product.
/// </summary>
/// <remarks>
/// Ledger data hangs off this row through <c>tenant_id</c> on every table, enforced by Postgres
/// row-level security rather than by remembering a <c>WHERE</c> clause. The legacy build had no
/// tenancy column at all — any Admin or Manager could read every company's ledger, and the
/// trial-balance cache key carried no tenant, so a second tenant would have been served the first
/// one's financial report straight out of Redis.
/// </remarks>
public class Tenant
{
    /// <summary>
    /// A <see cref="TenantId"/>, not a bare <c>Guid</c>: every dependent table's <c>TenantId</c> foreign
    /// key must be type-compatible with this principal key, and the domain wrapper is what stops an
    /// empty guid from being a valid tenant.
    /// </summary>
    public required TenantId Id { get; set; }

    /// <summary>Unique, url-safe, immutable once issued: it appears in cache keys and file paths.</summary>
    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>Bangla-first for this market; English stays available for formal reports.</summary>
    public string PreferredLanguage { get; set; } = DefaultLanguage;

    /// <summary>ISO 4217. Functional currency of the ledger; every GL line also stores its own.</summary>
    public string FunctionalCurrencyCode { get; set; } = DefaultCurrency;

    /// <summary>Fiscal year starts 1 July for this market. Period locking and numbering key off it.</summary>
    public int FiscalYearStartMonth { get; set; } = DefaultFiscalYearStartMonth;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public const string DefaultLanguage = "bn";
    public const string DefaultCurrency = "BDT";
    public const int DefaultFiscalYearStartMonth = 7;
}
