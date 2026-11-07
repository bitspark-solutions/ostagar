using Ostagar.Core;

namespace Ostagar.Platform.Tenancy;

/// <summary>
/// The one company record a tenant owns: what appears on its letterhead, its invoices and its statutory
/// filings. Separate from <see cref="Tenant"/> because that row is platform plumbing — slug, locale,
/// currency — while this is the customer's own data, edited by the customer's own staff.
/// </summary>
public class CompanyProfile : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>Registered legal name. Searched and sorted by hand, so it carries the Bangla collation.</summary>
    public required string LegalName { get; set; }

    public string LegalNameBangla { get; set; } = string.Empty;

    public string TradingName { get; set; } = string.Empty;

    /// <summary>Bangladesh BIN or e-TIN. An identifier, so it keeps the deterministic byte collation.</summary>
    public string TaxIdentificationNumber { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string ContactNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
