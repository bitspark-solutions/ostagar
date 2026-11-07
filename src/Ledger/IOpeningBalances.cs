namespace Ostagar.Ledger;

/// <summary>
/// Posts a company's opening balances as one journal entry.
/// </summary>
/// <remarks>
/// Declared separately from <see cref="IPoster"/> because the rules are different in kind. An ordinary
/// posting is balanced by what the document says; opening balances are balanced by <em>construction</em>,
/// from a trial balance an owner handed over, and the difference belongs to the owner's own money. A
/// caller that used <see cref="IPoster"/> directly would have to compute those sides itself, and the one
/// calculation a first-run import gets wrong is the one every later figure inherits.
/// </remarks>
public interface IOpeningBalances
{
    /// <summary>
    /// Posts the balances into period one of <paramref name="fiscalYear"/>.
    /// </summary>
    /// <remarks>
    /// A company has <b>one</b> opening entry. Once posted it is immutable like any other, and correcting
    /// the founding figures means reversing it and posting again — which is deliberate: a business's
    /// opening balance sheet is the assumption under every report drawn from it, and it must not be
    /// quietly re-derived until the numbers reconcile.
    /// </remarks>
    Task<PostingOutcome> PostAsync(
        int fiscalYear,
        IReadOnlyList<OpeningBalance> balances,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
