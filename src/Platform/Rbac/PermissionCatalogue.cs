namespace Ostagar.Platform.Rbac;

/// <summary>
/// The single source of truth for what can be authorised. The migration seeds these rows and the
/// provisioner grants them to roles, so a permission that is not written down here does not exist
/// anywhere.
///
/// <b>How these keys are enforced today.</b> Endpoints declare <c>[Authorize(Roles = ...)]</c> and the
/// role is read from the token. Nothing yet demands a permission key at the gate, so <c>(resource,
/// action)</c> is currently the vocabulary for <em>granting</em> a role, not the check itself. An
/// <c>IPermissionRequirement</c> handler that turns a key into a policy is the missing half; until it
/// exists, this comment says what is true rather than what the design intends, because a doc comment
/// describing an unbuilt component is how <c>JournalType.Opening</c> sat referenced by nothing.
/// </summary>
/// <remarks>
/// A <c>(resource, action)</c> pair rather than ad-hoc role names, because the buyer's staff is hired
/// by function: the accounts clerk needs <c>company.update</c> and nothing else, and that decision must
/// survive the clerk also being called "Senior Officer" in another factory.
/// </remarks>
public static class PermissionCatalogue
{
    public const string AnyResource = "*";
    public const string AnyAction = "*";
    public const string WildcardKey = AnyResource + "." + AnyAction;

    private static readonly PermissionDefinition[] Definitions =
    [
        new("tenant", "read", "See the tenant record this account belongs to."),
        new("company", "read", "Read the company profile: legal name, TIN, address."),
        new("company", "update", "Change the company profile that appears on documents."),
        new("department", "read", "List departments and open one by identifier."),
        new("department", "create", "Add a department."),
        new("department", "update", "Rename, reorder or activate a department."),
        new("department", "delete", "Remove a department."),
        new("user", "read", "List the tenant's users and open one by identifier."),
        new("user", "create", "Invite a user into this tenant."),
        new("user", "update", "Change a user's name, state or role assignments."),
        new("user", "delete", "Remove a user from this tenant."),
        new("role", "read", "List roles and the permissions each one holds."),
        new("role", "create", "Add a custom role."),
        new("role", "update", "Change a role's permissions."),
        new("role", "delete", "Remove a custom role."),
        new("permission", "read", "Read the permission catalogue."),
        new("audit", "read", "Read the tenant's audit trail."),
        new("journal", "read", "List and open the tenant's general-ledger entries."),
        new("journal", "create", "Write a draft or manual journal entry."),
        new("journal", "post", "Make an entry permanent — the act that ends its editability."),
        new("journal", "reverse", "Correct a posted entry by posting its mirror."),
        new("journal", "open_balance", "Found a company with its opening balances."),
        new("rate", "read", "Read the tenant's exchange-rate history."),
        new("rate", "record", "Append an effective-dated exchange rate."),
        new("ledger", "provision", "Create a company's periods, chart of accounts and journals."),
    ];

    public static IReadOnlyList<PermissionDefinition> All => Definitions;

    public static string Key(string resource, string action) => $"{resource}.{action}";

    public static bool TryResolve(string key, out PermissionDefinition definition)
    {
        var (resource, action) = Split(key);
        foreach (var candidate in Definitions)
        {
            if (string.Equals(candidate.Resource, resource, StringComparison.Ordinal)
                && string.Equals(candidate.Action, action, StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null!;
        return false;
    }

    public static bool IsKnown(string resource, string action) =>
        Definitions.Any(definition =>
            string.Equals(definition.Resource, resource, StringComparison.Ordinal)
            && string.Equals(definition.Action, action, StringComparison.Ordinal));

    private static (string Resource, string Action) Split(string key)
    {
        var separator = key.IndexOf('.', StringComparison.Ordinal);
        return separator < 0
            ? (key, AnyAction)
            : (key[..separator], key[(separator + 1)..]);
    }
}
