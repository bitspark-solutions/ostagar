using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One live counter for one scheme in one period: the row the integrator locks before it takes a number.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="DocumentNumberingScheme"/> because the two change for different reasons
/// and only one of them is editable by a tenant. A scheme edit must never move a counter, and a
/// counter move must never be able to change how numbers are rendered.
///
/// <see cref="LastAllocationKey"/> and <see cref="LastAllocatedNumber"/> together are what make a retry
/// safe: a document that timed out after allocating re-reads this row, finds its own key already
/// recorded, and is handed back the number it already consumed instead of a second one. That is the
/// same idempotency idea as ledger invariant I5, applied before the ledger is ever reached.
///
/// <see cref="IssuedCount"/> exists so that "has this scheme issued anything?" is a fact on the row
/// rather than a query over documents, where a deleted document would quietly answer no.
/// </remarks>
public sealed record NumberingSequenceState : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>The scheme whose rendering rules this counter follows.</summary>
    public Guid SchemeId { get; set; }

    /// <summary>Denormalised so a stale or mis-keyed row is detectable instead of silent.</summary>
    public required DocumentTypeCode DocumentType { get; init; }

    /// <summary>The period this counter belongs to; <see cref="NumberingPeriodKey.Code"/> for the component.</summary>
    public required string PeriodKey { get; init; }

    /// <summary>The next value to hand out. Advanced by exactly one per allocation, never otherwise.</summary>
    public int NextValue { get; init; } = 1;

    /// <summary>How many numbers this counter has handed out, including documents later cancelled.</summary>
    public int IssuedCount { get; init; }

    /// <summary>Caller-supplied key of the last allocation, or null when nothing has been allocated.</summary>
    public string? LastAllocationKey { get; init; }

    /// <summary>The number that key received, kept so a replay returns it instead of a new one.</summary>
    public string? LastAllocatedNumber { get; init; }
}
