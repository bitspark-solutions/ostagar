using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// A posting channel, and the thing that owns an entry-number sequence.
/// </summary>
/// <remarks>
/// Numbering is per (tenant, journal, fiscal period) and gapless. NetSuite's GL Audit Numbering is the
/// reference behaviour: a voided transaction still consumes a number, and once a number is assigned it
/// cannot be changed. Gaps in a voucher sequence are what an auditor asks about first, and the legacy
/// tree had the opposite problem — <c>GenerateJournalNumberAsync</c> read the current maximum and
/// incremented it with no transaction and no lock, ordering by a <b>string</b> so that
/// <c>JE-2025-01-9</c> sorted above <c>JE-2025-01-10</c>, while the index backing it was non-unique.
/// Two concurrent postings could therefore receive the same voucher number.
/// </remarks>
public class Journal : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>Stable machine code, e.g. <c>SAL</c>, <c>PUR</c>, <c>CASH</c>, <c>JOURNAL</c>.</summary>
    public required string Code { get; set; }

    public required string Name { get; set; }

    public string NameBangla { get; set; } = string.Empty;

    /// <summary>Prefix for generated numbers. Kept short because the number is quoted on paper.</summary>
    public required string NumberPrefix { get; set; }

    /// <summary>Default debit account for this channel, resolved by the posting engine.</summary>
    public Guid? DefaultDebitAccountId { get; set; }

    /// <summary>Default credit account for this channel.</summary>
    public Guid? DefaultCreditAccountId { get; set; }

    /// <summary>
    /// Whether this channel posts on the first of the month when no date is given. Cash books generally
    /// do not; recurring journals sometimes do.
    /// </summary>
    public bool AllowsDrafts { get; set; } = true;

    /// <summary>Storno reversals for this channel. Some tax regimes require it; most do not.</summary>
    public bool UsesStorno { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Next number to issue. Read under <c>SELECT … FOR UPDATE</c> inside the posting transaction, so
    /// two concurrent postings cannot take the same one.
    /// </summary>
    public int NextNumber { get; set; } = 1;
}
