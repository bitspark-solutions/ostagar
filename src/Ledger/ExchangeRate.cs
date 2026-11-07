using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// The rate at which one currency converted into the company's own, from a date forward.
/// </summary>
/// <remarks>
/// <para>
/// <b>Effective-dated rather than current.</b> A single "rate today" column makes every historical
/// statement a lie as soon as it moves: re-run last month's reports in June and the export invoice is
/// worth a different amount, and the trial balance no longer reconciles to the one that was filed. The
/// question a ledger asks is never "what is it worth now" but "what did it cost on the day", and that
/// answer cannot change after the fact.
/// </para>
///
/// <para>
/// <b>One row per currency per effective date, never edited.</b> A rate is history the moment it is
/// recorded, so correcting a wrong one means a row with the same <see cref="ValidFrom"/> — which the
/// unique index refuses — or a new date. That friction is deliberate: a rate that has already been used
/// by a posted line cannot be restated underneath it without the posting and its source disagreeing,
/// and a ledger must not be able to reach that state quietly.
/// </para>
///
/// <para>
/// There is no <c>IsActive</c> or soft delete. Deactivating a historical rate is the same act as
/// editing it, described differently.
/// </para>
/// </remarks>
public class ExchangeRate : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>The currency being priced, never the company's own — a rate for BDT would be 1 by definition.</summary>
    public CurrencyCode Currency { get; set; }

    /// <summary>First day this rate applies, inclusive, and the row is looked up by it rather than by its own id.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Units of functional currency per one unit of <see cref="Currency"/>.</summary>
    public decimal Rate { get; set; }

    /// <summary>Where the number came from — <c>manual</c>, <c>central-bank</c> — because a rate with no source is unfalsifiable.</summary>
    public string Source { get; set; } = string.Empty;

    public DateTimeOffset RecordedAt { get; set; }

    public Guid? RecordedByUserId { get; set; }

    /// <summary>
    /// Whether this row is the one to use for a date: it applies from its own day until the next row for
    /// the same currency begins.
    /// </summary>
    public bool AppliesOn(DateOnly date) => ValidFrom <= date;
}
