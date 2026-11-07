using Ostagar.Core;

namespace Ostagar.Platform.Tenancy;

/// <summary>
/// A cost-bearing section of a factory — cutting, sewing, finishing, packing, stores, accounts. Payroll
/// and production both key off it, so it is tenant data created once and referenced everywhere.
/// </summary>
/// <remarks>
/// The code is the stable handle an operator types, and it is unique per tenant rather than globally:
/// two factories both having a SEW section is normal, and a global constraint would make tenant B's
/// insert fail with a duplicate-key error that proves tenant A exists.
/// </remarks>
public class Department : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    public required string Code { get; set; }

    public required string Name { get; set; }

    public string NameBangla { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
