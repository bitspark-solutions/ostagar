using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Decides whether a document is safe to print, and hands the renderer the figures to print.
/// </summary>
/// <remarks>
/// <para>
/// <b>The predicate is a check, never a calculation of record.</b> Lines are summed only to compare with
/// the stored subtotal; the total that goes on the paper is the stored one. If the two disagree — by one
/// paisa, which is the smallest difference there is and the one a tolerance would hide — the document is
/// refused rather than quietly printed with either figure. A report that recomputes is a report that can
/// disagree with the document it prints, and the legacy build proved it: three trial-balance engines,
/// three answers, one database.
/// </para>
/// <para>
/// <b>No tolerance anywhere.</b> Money is decimal at the currency's scale, so equality is exact and
/// "close enough" has no meaning here. The old validator's ±0.01 was one whole minor unit — precisely the
/// width of a paisa that went missing.
/// </para>
/// <para>
/// <b>Both languages or no print.</b> A status label without a Bangla form is refused, because the
/// document that leaves the factory is the one a Bangla-reading counterparty signs.
/// </para>
/// </remarks>
public static class DocumentPrintReadiness
{
    private static readonly IReadOnlyList<PrintReadinessError> NoErrors = [];

    /// <summary>
    /// Checks a document's own internal agreement: number, lines, and the five stored totals.
    /// </summary>
    public static PrintReadinessDecision Check(PrintableDocument document) => Evaluate(document, null);

    /// <summary>
    /// Also checks that the state printed on the document is a state its workflow definition declares, and
    /// that the label printed is that state's label.
    /// </summary>
    /// <remarks>
    /// Without this second form, a print path can hand over any string as the status, and a document that
    /// says <c>APPROVED</c> while the machine has it as <c>RETURNED</c> prints perfectly. The status a
    /// buyer reads is a claim about the workflow, so it is checked against the workflow.
    /// </remarks>
    public static PrintReadinessDecision Check(PrintableDocument document, DocumentWorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return Evaluate(document, definition);
    }

    private static PrintReadinessDecision Evaluate(
        PrintableDocument document,
        DocumentWorkflowDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<PrintReadinessError>();

        if (string.IsNullOrWhiteSpace(document.Number))
        {
            errors.Add(new PrintReadinessError(
                PrintReadinessErrorCode.MissingNumber,
                "This document has no number, so it has not been registered in its series; printing it would put an unnumbered document into circulation."));
        }

        if (document.Lines.Count == 0)
        {
            errors.Add(new PrintReadinessError(
                PrintReadinessErrorCode.NoLines,
                "There is nothing on this document to print."));
        }

        for (var index = 0; index < document.Lines.Count; index++)
        {
            var line = document.Lines[index];

            if (string.IsNullOrWhiteSpace(line.Description) && string.IsNullOrWhiteSpace(line.DescriptionBangla))
            {
                errors.Add(new PrintReadinessError(
                    PrintReadinessErrorCode.LineDescriptionMissing,
                    "A printed line needs text in at least one language.",
                    line.Position == 0 ? index + 1 : line.Position));
            }
        }

        if (!document.StatusLabel.IsComplete)
        {
            errors.Add(new PrintReadinessError(
                PrintReadinessErrorCode.StatusLabelIncomplete,
                $"'{document.StateCode}' has no complete English and Bangla label, so the status on this print would be only half the sentence."));
        }

        if (definition is not null)
        {
            var state = definition.FindState(document.StateCode);

            if (state is null)
            {
                errors.Add(new PrintReadinessError(
                    PrintReadinessErrorCode.UnknownPrintedState,
                    $"'{document.StateCode}' is not a state of {document.DocumentType}, so the status printed here is not a status the workflow knows."));
            }
            else if (state.Label != document.StatusLabel)
            {
                errors.Add(new PrintReadinessError(
                    PrintReadinessErrorCode.StatusLabelDoesNotMatchState,
                    $"'{document.StateCode}' is printed as '{document.StatusLabel}', but the definition says '{state.Label}'. The paper must quote the workflow, not a copy of it."));
            }
        }

        var currencies = new HashSet<Currency>();
        currencies.Add(document.Subtotal.Currency);
        currencies.Add(document.TaxTotal.Currency);
        currencies.Add(document.GrandTotal.Currency);
        currencies.Add(document.PaidTotal.Currency);
        currencies.Add(document.DueTotal.Currency);

        foreach (var line in document.Lines)
        {
            currencies.Add(line.Amount.Currency);
        }

        if (currencies.Count > 1)
        {
            errors.Add(new PrintReadinessError(
                PrintReadinessErrorCode.MixedCurrencies,
                "A document carries amounts in more than one currency, so its printed total names no currency. Split it or convert with an explicit rate."));
        }

        // The arithmetic below only runs when every amount shares one currency: Money refuses to add
        // across currencies, and a refusal here would be an exception on a print screen instead of a
        // finding the operator can act on. It runs even when a header defect is already known, because an
        // operator at a printer wants every problem with the document in one report.
        if (currencies.Count == 1)
        {
            var currency = document.Subtotal.Currency;
            var lineSum = Money.Zero(currency);
            var lineCount = 0;

            foreach (var line in document.Lines)
            {
                lineSum = Money.Add(lineSum, line.Amount);
                lineCount++;
            }

            if (lineSum != document.Subtotal)
            {
                errors.Add(new PrintReadinessError(
                    PrintReadinessErrorCode.StatedSubtotalDisagreesWithLines,
                    $"The document states {document.Subtotal} but its {lineCount} line(s) come to {lineSum}. Neither figure may be printed until the document is corrected, because the paper cannot carry two answers."));
            }

            if (Money.Add(document.Subtotal, document.TaxTotal) != document.GrandTotal)
            {
                errors.Add(new PrintReadinessError(
                    PrintReadinessErrorCode.GrandTotalDoesNotCompose,
                    $"Subtotal {document.Subtotal} plus tax {document.TaxTotal} is not the stated total {document.GrandTotal}."));
            }

            if (Money.Add(document.PaidTotal, document.DueTotal) != document.GrandTotal)
            {
                errors.Add(new PrintReadinessError(
                    PrintReadinessErrorCode.BalanceDoesNotSettle,
                    $"Paid {document.PaidTotal} plus due {document.DueTotal} is not the stated total {document.GrandTotal}, so the printed balance would be wrong whatever it says."));
            }
        }

        if (errors.Count > 0)
        {
            return new PrintReadinessDecision(false, errors);
        }

        return new PrintReadinessDecision(
            true,
            NoErrors,
            document.GrandTotal,
            document.DueTotal,
            document.StatusLabel);
    }
}
