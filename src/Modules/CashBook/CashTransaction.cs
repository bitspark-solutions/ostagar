using Ostagar.Core;

namespace Ostagar.Modules.CashBook;

/// <summary>
/// A recorded cash movement — the source document whose posting the general ledger holds.
/// </summary>
/// <remarks>
/// <para>
/// The document stores the amount split across <see cref="Amount"/> and <see cref="Currency"/> rather
/// than as <see cref="Money"/>, the same choice <c>GlLine</c> makes: the row is a fact about what was
/// written, and reconstructing the typed amount at the boundary keeps the mapping honest without a
/// value-object converter deciding scale silently.
/// </para>
///
/// <para>
/// Corrections do not edit this row. A wrong transaction is reversed — the ledger posts a mirror entry
/// and this document records the link — so an auditor reading the day sees both the mistake and its
/// correction rather than a figure that quietly changed.
/// </para>
/// </remarks>
public class CashTransaction : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    public CashTransactionKind Kind { get; set; }

    public CashTransactionStatus Status { get; set; } = CashTransactionStatus.Posted;

    public DateOnly TransactionDate { get; set; }

    /// <summary>The company's own cash or bank account. Subtype decides the journal, not the name.</summary>
    public Guid CashAccountId { get; set; }

    /// <summary>The account the movement is for.</summary>
    public Guid CounterAccountId { get; set; }

    /// <summary>The amount as counted, positive, in <see cref="Currency"/>.</summary>
    public decimal Amount { get; set; }

    public CurrencyCode Currency { get; set; } = CurrencyCode.Bdt;

    public Guid? PartnerId { get; set; }

    public string Memo { get; set; } = string.Empty;

    /// <summary>
    /// The user this document is attributed to, declared by the caller rather than read from ambient
    /// state — the same rule the posting request enforces, so a retry or a CLI import cannot write
    /// money with no name against it.
    /// </summary>
    public Guid ActorUserId { get; set; }

    /// <summary>
    /// The document's revision for idempotent posting (invariant I5). A retry of the same document at
    /// the same iteration resolves to the same entry instead of posting twice.
    /// </summary>
    public int Iteration { get; set; } = 1;

    /// <summary>The posted entry this document produced. Null only before the write lands.</summary>
    public Guid? PostedEntryId { get; set; }

    /// <summary>The reversal entry that corrected this document, when it has been corrected.</summary>
    public Guid? ReversedByEntryId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The amount re-typed. Callers that need arithmetic take it through here.</summary>
    public Money TypedAmount() => new(Core.Currency.Of(Currency), Amount);
}
