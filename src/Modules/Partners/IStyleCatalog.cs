namespace Ostagar.Modules.Partners;

/// <summary>
/// Confirms that a style referenced by id is a real style in this tenant's catalogue.
/// </summary>
/// <remarks>
/// A price list line names a style, and the only honest ways to hold that reference are a copy or an id. A
/// copy drifts the moment Inventory renames or retires the style, and an id nobody checks lets a price be
/// agreed against a style that does not exist — which surfaces as an order line that cannot be dispatched,
/// far away from the master-data screen that accepted it.
///
/// So this module holds the id and asks. The implementation is Inventory's (P5), and the check runs in the
/// service layer that persists a price list, where the database's foreign key is the same rule enforced a
/// second time. Nothing here pretends to have verified a style by accepting a GUID.
/// </remarks>
public interface IStyleCatalog
{
    /// <summary>True when the tenant owns a style row with that id. Absent is absent; the call does not create one.</summary>
    Task<bool> StyleExistsAsync(
        StyleId style,
        CancellationToken cancellationToken = default);
}
