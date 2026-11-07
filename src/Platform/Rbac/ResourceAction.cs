namespace Ostagar.Platform.Rbac;

/// <summary>
/// One persisted <c>(resource, action)</c> pair: the thing a role can be granted.
/// </summary>
/// <remarks>
/// Global reference data, deliberately not tenant-scoped. A permission says nothing about any
/// company's money, so every tenant may read the catalogue; what differs between tenants is which
/// roles hold which permission, and that is <see cref="RoleGrant"/>, which is tenant-owned. The legacy
/// build seeded none of this, so every <c>[Authorize]</c> in the product was decoration over an empty
/// table.
/// </remarks>
public class ResourceAction
{
    public int Id { get; set; }

    /// <summary>What is being touched: <c>department</c>, <c>user</c>, <c>resource_actions</c>.</summary>
    public required string Resource { get; set; }

    /// <summary>What may be done to it: <c>read</c>, <c>create</c>, <c>update</c>, <c>delete</c>.</summary>
    public required string Action { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Key => PermissionCatalogue.Key(Resource, Action);
}
