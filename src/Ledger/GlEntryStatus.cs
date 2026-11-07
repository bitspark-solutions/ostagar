namespace Ostagar.Ledger;

/// <summary>
/// How far an entry has got. Deliberately only two values.
/// </summary>
/// <remarks>
/// There is no <c>Reversed</c> status. Reversal is a second entry that links back to the first, not a
/// flag on the original — flipping a flag leaves the amounts in the ledger, which is precisely the bug
/// in the legacy system where delete and reverse were the same one-line status change and a
/// "reversed" entry kept being summed.
///
/// Nor is there an <c>Approved</c> state distinct from <c>Posted</c>. The legacy trial balance filtered
/// on <c>Status == Posted</c> while its cash-book completion path set <c>Approved</c>, so every
/// properly-validated double-entry was invisible to the report and every single-sided unbalanced one
/// was included.
/// </remarks>
public enum GlEntryStatus
{
    /// <summary>Edit freely. Consumes no entry number and is invisible to every report.</summary>
    Draft,

    /// <summary>Immutable. Row-level triggers refuse UPDATE and DELETE from here on.</summary>
    Posted,
}
