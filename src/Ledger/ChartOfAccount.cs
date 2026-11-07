using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// One line of a company's chart of accounts.
/// </summary>
/// <remarks>
/// The hierarchy is adjacency (<see cref="ParentId"/>) plus a materialised <c>ltree</c> path
/// (<see cref="Path"/>). Adjacency alone forces a recursive query per report; a hand-rolled nested set
/// makes every insert a rewrite. The legacy tree had a <c>ParentAccountId</c> column that nothing ever
/// wrote and nothing ever read, so there was no hierarchy at all and every subtotal was grouped flat by
/// the five-value <c>AccountType</c> enum.
///
/// Parents are <b>not postable</b>. ERPNext rejects GL entries on group accounts for the same reason:
/// if a total and its own components can all receive postings, the trial balance is ambiguous and no
/// constraint will ever tell you which reading was intended.
/// </remarks>
public class ChartOfAccount : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>
    /// Company-facing account code, unique per tenant. Not a GUID prefix: the legacy grid printed a
    /// truncated GUID under a header that said "CODE".
    /// </summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    /// <summary>Bangla display name. Optional, because account codes stay Latin in this market.</summary>
    public string NameBangla { get; set; } = string.Empty;

    public AccountType Type { get; set; }

    /// <summary>
    /// Immutable once the account has been posted to. Changing it reclassifies every historical
    /// balance without touching the ledger, which is how reports silently stop being true.
    /// </summary>
    public AccountSubType SubType { get; set; }

    public Guid? ParentId { get; set; }

    /// <summary>
    /// The ltree path, e.g. <c>assets.current.assets_receivable</c>. Kept as a string here and mapped
    /// to the <c>ltree</c> type in the database, so C# never has to model a Postgres-only type.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>True for leaves only. A parent with children cannot receive postings.</summary>
    public bool IsPostable { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Recomputes the path from a parent's path. Called by the service layer rather than a trigger so
    /// the rule is testable in C#; a database trigger mirrors it for safety against direct writes.
    /// </summary>
    public void RelocateUnder(string? parentPath, string parentSegment)
    {
        Path = string.IsNullOrEmpty(parentPath)
            ? parentSegment
            : $"{parentPath}.{parentSegment}";
    }
}
