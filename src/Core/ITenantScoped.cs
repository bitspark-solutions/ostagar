namespace Ostagar.Core;

/// <summary>
/// Marks an entity whose rows belong to exactly one tenant. The model builder reads this marker to
/// give the table its tenancy column, its composite index and its check constraint, and the database
/// reads the resulting column for row-level security.
/// </summary>
/// <remarks>
/// The tenant directory itself deliberately does not implement it. It is the one table that must be
/// readable before a tenant is known — otherwise logging in would need the tenant it is trying to
/// establish.
///
/// Lives in Core rather than beside the entities that implement it, because every module that owns a
/// tenant-owned table must mark it and none of them may depend on the platform. Placing the marker
/// anywhere deeper than the primitives is what fused the entity projects to the persistence layer.
/// </remarks>
public interface ITenantScoped
{
    TenantId TenantId { get; set; }
}
