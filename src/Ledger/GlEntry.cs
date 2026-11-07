using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// The header of a general-ledger posting.
/// </summary>
/// <remarks>
/// A journal entry is append-only once posted. Every correction is a new entry pointing back at the
/// original through <see cref="ReversesEntryId"/> — never an edit, and never a status flag that leaves
/// the money in the books.
///
/// That last distinction is the specific defect this design refuses: the legacy
/// <c>DeleteJournalEntryAsync</c> and <c>ReverseJournalEntryAsync</c> had identical bodies, both just
/// <c>Status = Reversed</c>. The lines kept their amounts, so a "reversed" entry kept flowing into the
/// trial balance while the UI showed it as gone.
/// </remarks>
public class GlEntry : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>Gapless per (tenant, journal, period). Minted at post time and never reused.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Which posting channel this came through: sales, purchases, cash book, manual, stock.</summary>
    public required string JournalCode { get; set; }

    public Guid PeriodId { get; set; }

    /// <summary>The date the entry affects the books, which is what periods and locks are evaluated on.</summary>
    public DateOnly PostingDate { get; set; }

    /// <summary>When it was actually entered. Backdated postings are legal and must be visible as such.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Free-text narration, in the operator's language.</summary>
    public string Memo { get; set; } = string.Empty;

    /// <summary>
    /// What produced this entry — <c>sales_invoice</c>, <c>vendor_bill</c>, <c>cash_book_entry</c>.
    /// Together with <see cref="SourceId"/> and <see cref="Iteration"/> this is the idempotency key.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>The producing document's identifier, or empty for a hand-written journal.</summary>
    public Guid? SourceId { get; set; }

    /// <summary>
    /// Increments only when a document is deliberately re-posted after a rule change. A retry of the
    /// same (type, id, iteration) is rejected rather than duplicated, which is what makes posting
    /// safe to retry after a timeout — and re-posting legal, because the old rows are reversed rather
    /// than edited.
    /// </summary>
    public int Iteration { get; set; } = 1;

    public GlEntryStatus Status { get; set; }

    /// <summary>Set when this entry exists to undo another. Both stay in the books.</summary>
    public Guid? ReversesEntryId { get; set; }

    /// <summary>
    /// Storno reversal: the reversing amount is written as a negative on its original side rather than
    /// flipped to the opposite side. Required by some tax regimes because a flipped reversal inflates
    /// gross debit and credit turnover.
    /// </summary>
    public bool UsesStorno { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? PostedByUserId { get; set; }

    public List<GlLine> Lines { get; set; } = [];
}
