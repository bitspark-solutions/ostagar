namespace Ostagar.Platform.Rbac;

/// <summary>
/// The permission set each system role is created with. A factory with three people uses two of these
/// rows and a factory with three hundred uses all five, so the roles are named for the function rather
/// than for the headcount.
/// </summary>
/// <remarks>
/// Owner holds the wildcard instead of a copy of the catalogue: a tenant must always be able to
/// re-grant itself after a later phase adds permissions, without a migration that retro-fits rights
/// onto existing tenants and silently changes who can see whose ledger.
/// </remarks>
public static class DefaultRoleMatrix
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Accounts = "Accounts";
    public const string Stock = "Stock";
    public const string Production = "Production";

    public static readonly IReadOnlyList<string> RoleNames = [Owner, Manager, Accounts, Stock, Production];

    private static readonly Dictionary<string, string[]> Grants = new(StringComparer.Ordinal)
    {
        [Owner] = [PermissionCatalogue.WildcardKey],
        [Manager] =
        [
            "tenant.read", "company.read", "company.update",
            "department.read", "department.create", "department.update",
            "user.read", "user.create", "user.update",
            "role.read", "permission.read", "audit.read",
        ],
        // The accounts clerk is the person who actually keeps a factory's books, so the ledger keys are
        // granted here and nowhere else above Owner. Before this the Accounts role could read the company
        // profile and nothing about its accounts — which is why P3's endpoints had to be reachable by
        // Owner alone, and an owner who is the only one able to post a journal is not a product, it is a
        // spreadsheet with a login screen.
        [Accounts] =
        [
            "tenant.read", "company.read", "company.update",
            "department.read", "user.read", "permission.read", "audit.read",
            "journal.read", "journal.create", "journal.post", "journal.reverse",
            "journal.open_balance", "rate.read", "rate.record",
        ],
        [Stock] =
        [
            "tenant.read", "company.read",
            "department.read", "department.create", "department.update", "permission.read",
        ],
        [Production] =
        [
            "tenant.read", "company.read",
            "department.read", "department.create", "department.update", "permission.read",
        ],
    };

    public static IReadOnlyList<string> PermissionsFor(string roleName) =>
        Grants.TryGetValue(roleName, out var permissions)
            ? permissions
            : throw new ArgumentException($"'{roleName}' is not a system role.", nameof(roleName));
}
