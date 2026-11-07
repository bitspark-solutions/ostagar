namespace Ostagar.Ledger;

/// <summary>
/// One account as a template says it should exist, before it is a row.
/// </summary>
/// <remarks>
/// A specification rather than an entity because a template is read-only input to provisioning: an
/// entity here would be a row someone could edit, and an edited chart template would change every
/// company provisioned afterwards while the existing ones kept the old accounts. That divergence is
/// invisible in a single tenant and obvious in the hundredth.
/// </remarks>
/// <param name="Code">Stable account code, unique within the company.</param>
/// <param name="Name">English display name, used for reports and account codes.</param>
/// <param name="NameBangla">Bangla display name.</param>
/// <param name="Type">Drives the normal balance and the statement the account reports on.</param>
/// <param name="SubType">Groups the account within its type.</param>
/// <param name="ParentCode">The grouping account above this one; null for a top-level account.</param>
/// <param name="IsPostable">False for a grouping account, which the schema then refuses to post to.</param>
public sealed record AccountSpec(
    string Code,
    string Name,
    string NameBangla,
    AccountType Type,
    AccountSubType SubType,
    string? ParentCode,
    bool IsPostable);
