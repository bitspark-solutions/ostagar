namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Machine-readable reasons a document is not safe to put on paper.
/// </summary>
public enum PrintReadinessErrorCode
{
    /// <summary>No number. The document was never registered in its series.</summary>
    MissingNumber,

    /// <summary>Nothing printed as an amount. An empty document is a bug wearing a header.</summary>
    NoLines,

    /// <summary>A line with no text to print on it.</summary>
    LineDescriptionMissing,

    /// <summary>Amounts in more than one currency on one document, so the printed total names no currency.</summary>
    MixedCurrencies,

    /// <summary>The stated subtotal and the sum of the lines are two different answers.</summary>
    StatedSubtotalDisagreesWithLines,

    /// <summary>Subtotal plus tax is not the stated grand total.</summary>
    GrandTotalDoesNotCompose,

    /// <summary>Paid plus due is not the stated grand total.</summary>
    BalanceDoesNotSettle,

    /// <summary>The state printed on the document is not one its workflow definition declares.</summary>
    UnknownPrintedState,

    /// <summary>The status label is not the label the definition gives this state, so the paper is quoting a stale or invented status.</summary>
    StatusLabelDoesNotMatchState,

    /// <summary>A status label in one language only. Law L17 refuses this on printed output.</summary>
    StatusLabelIncomplete,
}
