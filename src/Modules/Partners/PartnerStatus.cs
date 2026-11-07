namespace Ostagar.Modules.Partners;

/// <summary>
/// Whether a partner is open for new business. There is no third state and no deleted state.
/// </summary>
/// <remarks>
/// <see cref="Inactive"/> is a commercial pause, not a removal: the party's documents, payments and ledger
/// history all still exist and must still resolve to a name on an aged report. The legacy tree's habit of
/// "cleaning up" a duplicate by deleting the row is what leaves a posted journal line pointing at nothing,
/// so this module has no delete operation at all — a partner that is finished with is deactivated, keeps
/// its code and its identity, and refuses new documents.
/// </remarks>
public enum PartnerStatus
{
    /// <summary>New documents may reference this partner.</summary>
    Active,

    /// <summary>
    /// New documents may not reference this partner. Existing documents, balances and history are
    /// unaffected and still readable, which is why deactivating one with an open order is refused instead
    /// of silently orphaning it.
    /// </summary>
    Inactive,
}
