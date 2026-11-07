namespace Ostagar.Modules.CashBook;

/// <summary>
/// Machine-readable reasons a cash transaction is refused, so a client can translate them and a test
/// can assert on them rather than on prose.
/// </summary>
/// <remarks>
/// These are the cash book's own rules, checked before the ledger is asked. Balance, period locks and
/// account postability are the ledger's rules and arrive as <c>PostingErrorCode</c> through the
/// posting outcome — duplicating them here would be a second definition of the same invariant, which
/// is how the legacy tree accumulated three disagreeing notions of what a valid posting is.
/// </remarks>
public enum CashTransactionErrorCode
{
    /// <summary>No amount was given. A cash line without a figure is not a zero, it is unfinished.</summary>
    AmountMissing,

    /// <summary>
    /// Zero or negative. The direction lives in <see cref="CashTransactionKind"/>, so a signed amount
    /// would give one fact two places to say it — a negative payment and a positive receipt being the
    /// same thing is exactly the ambiguity the kind column exists to remove.
    /// </summary>
    AmountNotPositive,

    /// <summary>An account id was missing entirely.</summary>
    AccountMissing,

    /// <summary>
    /// The cash side and the counter side name the same account, which would post a balanced entry
    /// recording that money moved from a place to itself.
    /// </summary>
    SameAccountBothSides,

    /// <summary>
    /// The account named as the cash side is not a cash or bank account. Checked against the account's
    /// declared subtype — never its name, because the legacy tree classified by
    /// <c>accountNameLower.Contains("cash")</c> and let a renamed account move money between statement
    /// lines.
    /// </summary>
    CashAccountNotCashLike,

    /// <summary>
    /// A reversal was requested on a transaction that never posted. A document without an entry has
    /// nothing to correct.
    /// </summary>
    TransactionNotPosted,

    /// <summary>
    /// The transaction is already reversed. A second reversal would post a second mirror and undo the
    /// correction it is pretending to make.
    /// </summary>
    TransactionAlreadyReversed,
}
