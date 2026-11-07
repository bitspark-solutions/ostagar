namespace Ostagar.Ledger;

/// <summary>Machine-readable reasons a proposed posting is not valid.</summary>
/// <remarks>
/// Codes, not messages, so a client can translate them and a test can assert on them. The legacy
/// validator returned free-text strings and, worse, several different notions of "balanced" at once:
/// exact equality in some places and a ±0.01 tolerance in others, so an entry could save successfully
/// and then be reported as unbalanced by the next screen reading the same rows.
/// </remarks>
public enum PostingErrorCode
{
    /// <summary>Nothing was proposed. An empty posting is a bug, not a zero-amount entry.</summary>
    NoLines,

    /// <summary>
    /// Only one line. Rejected outright: this is the exact defect that made the legacy trial balance
    /// unable to balance, because the cash-book path wrote a single-sided entry straight to the ledger.
    /// </summary>
    SingleSided,

    /// <summary>A line has both sides, or neither.</summary>
    LineSides,

    /// <summary>A line has a zero amount on whichever side it uses.</summary>
    ZeroAmount,

    /// <summary>Total debits do not equal total credits. No tolerance is applied, ever.</summary>
    Unbalanced,

    /// <summary>
    /// Within one transaction currency the foreign-currency amounts do not offset. A ledger can be
    /// balanced in functional currency while remaining unbalanced in the currency actually invoiced,
    /// which is how an FX difference disappears without an entry recording it.
    /// </summary>
    UnbalancedInCurrency,

    /// <summary>Lines were proposed in more than one functional currency.</summary>
    MixedFunctionalCurrency,

    /// <summary>An exchange rate of zero or less, which cannot be inverted or explained.</summary>
    InvalidExchangeRate,

    /// <summary>
    /// A foreign-currency line whose rate the company's history does not cover for that date. Distinct
    /// from <see cref="InvalidExchangeRate"/>, which is a bad number: this is a missing one, and the fix
    /// is to record a rate rather than to correct one.
    /// </summary>
    MissingExchangeRate,

    /// <summary>
    /// A line whose functional amount does not follow from its foreign amount and its rate. The two are
    /// both stated and they disagree, which no rounding policy can account for.
    /// </summary>
    ExchangeRateAmountMismatch,

    /// <summary>
    /// The tenant does not own a journal with that code. Checked before the entry is built because the
    /// database refuses it too, through a foreign key, and a caller that gets a foreign-key violation
    /// here has been handed a schema detail instead of a sentence about their own document.
    /// </summary>
    JournalNotFound,

    /// <summary>
    /// No fiscal period covers the posting date. A year that was never provisioned is a different
    /// finding from a period someone forgot to open, and both differ from a locked one.
    /// </summary>
    PeriodNotFound,

    /// <summary>The period covering the posting date is locked. Lifting a lock is a separate, audited act.</summary>
    PeriodLocked,

    /// <summary>
    /// A line names an account the tenant does not own, or one that cannot be posted to. Account
    /// resolution happens before numbering, so a rejected posting does not consume a journal number.
    /// </summary>
    AccountUnpostable,

    /// <summary>
    /// A balance sheet named account codes the company does not have. Reported as one list rather than
    /// one error per line, because an opening balance sheet is typed from paper and a person correcting
    /// it wants the whole set of misread codes in one go.
    /// </summary>
    UnknownAccountCodes,

    /// <summary>The same account code appeared twice in one request, which is ambiguous rather than additive.</summary>
    DuplicateAccountCode,

    /// <summary>Nothing was given. A company with no opening balances still needs that said out loud.</summary>
    NoBalances,

    /// <summary>
    /// Nothing to reverse. A guessed entry id must not be answerable as "already reversed", because the
    /// difference tells a caller whether the entry they meant exists somewhere they cannot see.
    /// </summary>
    EntryNotFound,

    /// <summary>
    /// The entry is still a draft. A draft is corrected by editing it; only a posted entry needs a
    /// reversal, and reversing a draft would leave two unfinished entries pointing at each other.
    /// </summary>
    EntryNotPosted,

    /// <summary>The entry has already been reversed. A second reversal would double the correction.</summary>
    AlreadyReversed,

    /// <summary>
    /// The database refused the posting after the application had accepted it. Reported as its own code
    /// because it means the two layers disagree, which is a defect in this one regardless of which side
    /// is wrong.
    /// </summary>
    RefusedByDatabase,
}
