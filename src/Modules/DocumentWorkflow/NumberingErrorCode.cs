namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Machine-readable reasons a document number was not, or could not be, allocated.
/// </summary>
/// <remarks>
/// Codes rather than sentences for the same reason the ledger posts them: a caller can translate them
/// into Bangla, a test can assert on them, and a support ticket can quote one. The legacy tree returned
/// free-text strings from its validators, so two screens reported the same defect with different words
/// and neither could be matched to the other.
/// </remarks>
public enum NumberingErrorCode
{
    /// <summary>The prefix is empty, too long, or not in the fixed alphabet.</summary>
    InvalidPrefix,

    /// <summary>A padding width that no printed number can be read back from.</summary>
    WidthOutOfRange,

    /// <summary>A starting value below one, which would render zero or negative numbers.</summary>
    StartingValueBelowOne,

    /// <summary>A fiscal start month outside 1-12, which makes every period key a guess.</summary>
    InvalidFiscalStartMonth,

    /// <summary>The scheme is switched off. Refusing is correct; an off sequence must not emit numbers.</summary>
    SchemeInactive,

    /// <summary>No allocation key. Without it a retry is indistinguishable from a second document.</summary>
    AllocationKeyEmpty,

    /// <summary>
    /// The sequence row belongs to a different period than the document date asks for. Silent rollover
    /// here would restart a counter that is still in use and re-issue a live number.
    /// </summary>
    SequencePeriodMismatch,

    /// <summary>The sequence row and the scheme name different document types: a mis-wired row.</summary>
    SequenceDocumentTypeMismatch,

    /// <summary>
    /// The counter sits below the scheme's starting value. Only a rewind can do that, and a rewind
    /// re-issues numbers that documents already carry.
    /// </summary>
    CounterBelowStartingValue,

    /// <summary>
    /// The next value needs more digits than the width allows. Refused rather than widened or
    /// truncated: widening changes the printed length mid-sequence, truncating collides with a number
    /// already issued.
    /// </summary>
    ValueOutgrowsPadding,

    /// <summary>
    /// The caller expected a different counter than the row holds, so somebody allocated in between.
    /// Returning a second number for one document is the failure this refuses.
    /// </summary>
    StaleSequenceRead,

    /// <summary>
    /// The counter remembers an allocation key but not the number it gave. Only a half-written row can
    /// look like that, and the fix is to repair the row rather than to retry and hope.
    /// </summary>
    SequenceStateInconsistent,

    /// <summary>The scheme edit would move the prefix of numbers already in circulation.</summary>
    PrefixChangedAfterIssue,

    /// <summary>The scheme edit would narrow the printed width of a sequence that is already in use.</summary>
    WidthNarrowedAfterIssue,

    /// <summary>The edit would change which calendar span the sequence restarts on.</summary>
    PeriodComponentChangedAfterIssue,

    /// <summary>The edit would move the starting value of a sequence that has issued numbers.</summary>
    StartingValueChangedAfterIssue,

    /// <summary>The edit would re-cut the fiscal year, moving documents between periods.</summary>
    FiscalStartMonthChangedAfterIssue,

    /// <summary>The proposed width cannot render the counter the sequence has already reached.</summary>
    WidthNarrowerThanCounter,
}
