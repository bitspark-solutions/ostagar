namespace Ostagar.Modules.CashBook;

/// <summary>
/// Where the document stands. There is deliberately no Draft: a cash book line is written when the
/// money moves, and holding unposted cash rows is how the legacy tree ended up with a cash book that
/// disagreed with its own ledger by exactly the rows nobody had remembered to finish.
/// </summary>
/// <remarks>
/// <see cref="Reversed"/> is a terminal state reached only through a posted reversal entry, never by
/// editing. The original transaction keeps its date, its amount and its entry; the correction is a
/// second fact pointing back at it, because invariant I3 applies to the document's posting exactly as
/// it does to any other.
/// </remarks>
public enum CashTransactionStatus
{
    /// <summary>Written to the general ledger. <see cref="CashTransaction.PostedEntryId"/> names the entry.</summary>
    Posted,

    /// <summary>Corrected by a reversal entry. <see cref="CashTransaction.ReversedByEntryId"/> names it.</summary>
    Reversed,
}
