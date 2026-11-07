using Ostagar.Core;
using Ostagar.Modules.DocumentWorkflow;

namespace Ostagar.Tests.Modules.DocumentWorkflow;

/// <summary>
/// The print predicate, whose two jobs are that a document's printed total is the total it was stored with,
/// and that a document whose two accounts of itself disagree never reaches paper.
/// </summary>
public class DocumentPrintReadinessTests
{
    private static readonly IReadOnlyList<PrintDocumentLine> Lines =
    [
        new PrintDocumentLine(1, "30s single jersey, 180 gsm", "৩০সি সিঙ্গেল জার্সি", Money.Bdt(1000.00m)),
        new PrintDocumentLine(2, "Trim and buttons", "ট্রিম ও বোতাম", Money.Bdt(234.56m)),
    ];

    [Fact]
    public void A_document_whose_stored_totals_match_its_lines_is_safe_to_print()
    {
        var document = Document();

        var decision = DocumentPrintReadiness.Check(document, StandardDocumentWorkflows.SalesInvoice);

        Assert.True(decision.IsSafe);
        Assert.Empty(decision.Errors);

        // What the renderer is handed is the stored figure, not a sum: a reprint next year cannot arrive at
        // a different number because the lines were re-added by a later build.
        Assert.Equal(document.GrandTotal, decision.GrandTotal);
        Assert.Equal(document.DueTotal, decision.DueTotal);
        Assert.Equal("Approved", decision.StatusLabel!.Value.English);
    }

    [Fact]
    public void A_stated_total_one_paisa_out_from_its_own_lines_is_refused()
    {
        // The lines come to 1234.56. The document states 1234.55 and composes its grand total from that, so
        // every stored figure is internally consistent and only the comparison to the lines shows the
        // paisa. A tolerance of the legacy validator's kind — ±0.01, one whole minor unit — would have
        // printed it, which is exactly how a rounding policy that does not exist stayed hidden.
        var document = Document(subtotal: Money.Bdt(1234.55m), tax: Money.Bdt(76.88m));

        var decision = DocumentPrintReadiness.Check(document);

        Assert.False(decision.IsSafe);
        Assert.Contains(
            PrintReadinessErrorCode.StatedSubtotalDisagreesWithLines,
            decision.Errors.Select(error => error.Code));
        Assert.Null(decision.GrandTotal);
    }

    [Fact]
    public void A_grand_total_that_is_not_subtotal_plus_tax_is_refused()
    {
        var document = Document(grand: Money.Bdt(1311.44m));

        var codes = DocumentPrintReadiness.Check(document).Errors.Select(error => error.Code).ToList();

        Assert.Contains(PrintReadinessErrorCode.GrandTotalDoesNotCompose, codes);
        Assert.Contains(PrintReadinessErrorCode.BalanceDoesNotSettle, codes);
    }

    [Fact]
    public void A_paid_and_due_pair_that_does_not_add_up_to_the_total_is_refused()
    {
        var document = Document(paid: Money.Bdt(311.44m), due: Money.Bdt(1000.00m));

        Assert.Contains(
            PrintReadinessErrorCode.BalanceDoesNotSettle,
            DocumentPrintReadiness.Check(document).Errors.Select(error => error.Code));
    }

    [Fact]
    public void Amounts_in_two_currencies_are_refused_because_the_printed_total_would_name_neither()
    {
        var lines = new List<PrintDocumentLine>(Lines)
        {
            new PrintDocumentLine(3, "Freight, USD quoted", "ফ্রাইট", new Money(Currency.Of(CurrencyCode.Usd), 100.00m)),
        };

        var document = Document(lines: lines);

        Assert.Contains(
            PrintReadinessErrorCode.MixedCurrencies,
            DocumentPrintReadiness.Check(document).Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_document_that_was_never_given_a_number_is_not_printable()
    {
        var document = Document(number: "   ");

        Assert.Contains(
            PrintReadinessErrorCode.MissingNumber,
            DocumentPrintReadiness.Check(document).Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_status_label_missing_its_Bangla_form_is_refused_on_printed_output()
    {
        var document = Document(statusLabel: new BilingualLabel("Approved", " "));

        Assert.Contains(
            PrintReadinessErrorCode.StatusLabelIncomplete,
            DocumentPrintReadiness.Check(document).Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_status_the_workflow_does_not_declare_is_refused()
    {
        var document = Document(stateCode: "SHIPPED", statusLabel: new BilingualLabel("Shipped", "পাঠানো হয়েছে"));

        Assert.Contains(
            PrintReadinessErrorCode.UnknownPrintedState,
            DocumentPrintReadiness.Check(document, StandardDocumentWorkflows.SalesInvoice)
                .Errors
                .Select(error => error.Code));
    }

    [Fact]
    public void A_label_that_is_not_the_workflows_label_for_that_state_is_refused()
    {
        // The state is real and the label is complete, but they are not the pair the definition gives, so
        // the paper would be claiming a status the machine does not hold for it.
        var document = Document(statusLabel: StandardDocumentStates.Draft.Label);

        var decision = DocumentPrintReadiness.Check(document, StandardDocumentWorkflows.SalesInvoice);

        Assert.Contains(
            PrintReadinessErrorCode.StatusLabelDoesNotMatchState,
            decision.Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_document_with_nothing_on_it_is_refused_rather_than_printed_blank()
    {
        var document = Document(lines: [], subtotal: Money.Bdt(0m), tax: Money.Bdt(0m), grand: Money.Bdt(0m), paid: Money.Bdt(0m), due: Money.Bdt(0m));

        Assert.Contains(
            PrintReadinessErrorCode.NoLines,
            DocumentPrintReadiness.Check(document).Errors.Select(error => error.Code));
    }

    [Fact]
    public void A_line_with_no_text_is_reported_with_its_position()
    {
        var lines = new List<PrintDocumentLine>
        {
            new PrintDocumentLine(7, " ", " ", Money.Bdt(1000.00m)),
            new PrintDocumentLine(8, "Trim and buttons", "ট্রিম ও বোতাম", Money.Bdt(234.56m)),
        };

        var errors = DocumentPrintReadiness.Check(Document(lines: lines)).Errors;
        var lineError = errors.Single(error => error.Code == PrintReadinessErrorCode.LineDescriptionMissing);

        Assert.Equal(7, lineError.LinePosition);
    }

    [Fact]
    public void Every_defect_is_reported_in_one_pass_so_the_operator_fixes_the_document_once()
    {
        var document = Document(number: string.Empty, subtotal: Money.Bdt(1m), grand: Money.Bdt(2m), due: Money.Bdt(3m));

        var codes = DocumentPrintReadiness.Check(document).Errors.Select(error => error.Code).ToList();

        Assert.Contains(PrintReadinessErrorCode.MissingNumber, codes);
        Assert.Contains(PrintReadinessErrorCode.StatedSubtotalDisagreesWithLines, codes);
        Assert.Contains(PrintReadinessErrorCode.GrandTotalDoesNotCompose, codes);
        Assert.True(codes.Count > 2);
    }

    private static PrintableDocument Document(
        string number = "INV-2026-0001",
        string stateCode = DocumentStateCodes.Approved,
        BilingualLabel? statusLabel = null,
        Money? subtotal = null,
        Money? tax = null,
        Money? grand = null,
        Money? paid = null,
        Money? due = null,
        IReadOnlyList<PrintDocumentLine>? lines = null) =>
        new()
        {
            DocumentType = DocumentTypeCode.SalesInvoice,
            Number = number,
            StateCode = stateCode,
            StatusLabel = statusLabel ?? StandardDocumentStates.Approved.Label,
            DocumentDate = new DateOnly(2026, 10, 1),
            Subtotal = subtotal ?? Money.Bdt(1234.56m),
            TaxTotal = tax ?? Money.Bdt(76.87m),
            GrandTotal = grand ?? Money.Bdt(1311.43m),
            PaidTotal = paid ?? Money.Bdt(311.43m),
            DueTotal = due ?? Money.Bdt(1000.00m),
            Lines = lines ?? Lines,
        };
}
