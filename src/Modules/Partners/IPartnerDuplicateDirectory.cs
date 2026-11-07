using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// Supplies the stored identifiers that a duplicate check needs, without the check needing a database.
/// </summary>
/// <remarks>
/// Declared as the narrowest read the rule can work with: identifier values, tenant-scoped, and nothing else.
/// The implementation is the persistence layer's job (it is a query over the partner identifier table, plus
/// the bank-account and contact tables, all of which are indexed on the normalised value), and keeping it an
/// interface here is what lets the authoritative/suggestive split be tested without a Postgres.
///
/// It is deliberately a lookup by value rather than a search by name. A product that offered name search as
/// the duplicate control would put the decision back on a string a clerk typed, which is the defect this
/// module closes.
/// </remarks>
public interface IPartnerDuplicateDirectory
{
    /// <summary>
    /// Every stored identifier in <paramref name="tenant"/> whose value collides with one of
    /// <paramref name="candidates"/>. Returns nothing rather than guessing when no identifier matches.
    /// </summary>
    /// <remarks>
    /// The tenant is explicit rather than ambient. The same isolation rule that applies to every table applies
    /// to a duplicate check, and a check that ran across tenants would tell one company that a party with a
    /// given e-TIN already exists somewhere in the system — which leaks another tenant's customer list one
    /// error message at a time.
    /// </remarks>
    Task<IReadOnlyList<ExistingPartnerIdentifier>> FindMatchingAsync(
        TenantId tenant,
        IReadOnlyList<PartnerIdentifier> candidates,
        CancellationToken cancellationToken = default);
}
