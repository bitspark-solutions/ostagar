namespace Ostagar.Platform.Rbac;

/// <summary>
/// One entry of the permission catalogue, before it has a database identity.
/// </summary>
public sealed record PermissionDefinition(string Resource, string Action, string Description);
