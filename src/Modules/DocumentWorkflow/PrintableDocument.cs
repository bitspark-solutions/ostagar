using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The printable form of a document: its number, its status in both languages, its stored totals and the
/// lines beneath them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The totals here are the stored ones, read back.</b> That is the whole design of this record. A
/// report that re-sums the lines to print a total is a report that can disagree with the document it is
/// printing — and when it does, there is no way to tell which of the two the customer was charged, because
/// the paper is the only artefact that left the building. So
/// <see cref="Subtotal"/>, <see cref="TaxTotal"/>, <see cref="GrandTotal"/>, <see cref="PaidTotal"/> and
/// <see cref="DueTotal"/> are carried alongside the lines, and <see cref="DocumentPrintReadiness"/>
/// refuses the print when the two accounts of the same document differ. Agreement is checked;
/// recomputation is not allowed to substitute.
/// </para>
/// <para>
/// <b>Every total is <see cref="Money"/>.</b> A printed taka figure with no currency on it is the legacy
/// trial balance rendering <c>Math.abs</c> of a negative debit and labelling it USD.
/// </para>
/// <para>
/// <see cref="StatusLabel"/> is a <see cref="BilingualLabel"/> rather than a string, so a state that has no
/// Bangla name cannot reach the paper at all — law L17 enforced by a type instead of a review comment.
/// </para>
/// <para>
/// <see cref="Number"/> is never blank, because a document without one has not been registered in its
/// series; <see cref="DocumentDate"/> is the date on the paper, which is the date the numbering period was
/// derived from, and <see cref="StateCode"/> is checked against the document type's workflow definition so
/// that a print cannot quote a status nobody agreed to.
/// </para>
/// </remarks>
public sealed record PrintableDocument
{
    /// <summary>Which workflow definition governs the state being printed.</summary>
    public required DocumentTypeCode DocumentType { get; init; }

    /// <summary>The allocated document number.</summary>
    public required string Number { get; init; }

    /// <summary>The state code the document is in.</summary>
    public required string StateCode { get; init; }

    /// <summary>Both printed names of that state.</summary>
    public required BilingualLabel StatusLabel { get; init; }

    /// <summary>The date on the paper.</summary>
    public required DateOnly DocumentDate { get; init; }

    /// <summary>Stored net of tax.</summary>
    public required Money Subtotal { get; init; }

    /// <summary>Stored tax.</summary>
    public required Money TaxTotal { get; init; }

    /// <summary>Stored total the counterparty owes.</summary>
    public required Money GrandTotal { get; init; }

    /// <summary>Stored amount settled so far.</summary>
    public required Money PaidTotal { get; init; }

    /// <summary>Stored balance still outstanding.</summary>
    public required Money DueTotal { get; init; }

    /// <summary>The printed lines, in paper order.</summary>
    public required IReadOnlyList<PrintDocumentLine> Lines { get; init; }
}
