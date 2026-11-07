using System.Globalization;
using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Allocates business document numbers — orders, invoices, bills, notes, payroll runs.
/// </summary>
/// <remarks>
/// <para>
/// This is the numbering for <em>documents</em>, which is a different series from the ledger's journal
/// numbers and is not allowed to share one. A journal number is minted at post time by the posting
/// engine and proves that money moved; a document number is minted when the paper is created and proves
/// that the paper was registered. Collapsing them would mean an unapproved purchase order consumes a
/// voucher number, and the two questions an auditor asks — "which vouchers are missing" and "which
/// orders were never registered" — would stop having separate answers.
/// </para>
/// <para>
/// <b>No gaps, by construction and by absence.</b> The counter advances by exactly one per allocation and
/// this class has no <c>Release</c>, <c>Free</c>, <c>Recycle</c> or <c>Renumber</c> method, because a
/// cancelled document keeps the number it consumed. That is not tidiness: a gap made by deleting a
/// document is indistinguishable from a document that was never recorded, so the tenant cannot prove
/// which of the two happened, and neither can anyone else.
/// </para>
/// <para>
/// <b>Pure.</b> Nothing here reaches a database. <see cref="Allocate"/> takes the counter row as read
/// and returns the counter row as it must be saved, so the integrator's job is exactly one transaction:
/// lock the row, call this, write what comes back. There is no SQL in this project to get wrong, and the
/// rules stay testable — the legacy numbering had the SQL and the rule interleaved in one service and
/// nobody could say which of the two was lying.
/// </para>
/// </remarks>
public static class DocumentNumberAllocator
{
    private static readonly IReadOnlyList<NumberingError> NoErrors = [];

    /// <summary>
    /// The counter row for a period the scheme has not issued in yet, opened at the scheme's starting
    /// value.
    /// </summary>
    /// <remarks>
    /// A fresh period is opened explicitly rather than rolled over inside <see cref="Allocate"/>. A
    /// rollover that nobody asked for restarts a counter that may still be in use, and the collision it
    /// produces is invisible until two documents print the same number.
    ///
    /// <see cref="NumberingSequenceState.Id"/> is left empty on purpose: ids belong to the row the store
    /// writes, and a domain function that mints one reports a saved document that no one saved.
    /// </remarks>
    public static NumberingSequenceState OpenSequence(
        DocumentNumberingScheme scheme,
        NumberingPeriodKey period,
        TenantId tenantId)
    {
        ArgumentNullException.ThrowIfNull(scheme);

        if (tenantId.IsEmpty)
        {
            throw new ArgumentException("A sequence belongs to a tenant.", nameof(tenantId));
        }

        return new NumberingSequenceState
        {
            TenantId = tenantId,
            SchemeId = scheme.Id,
            DocumentType = scheme.DocumentType,
            PeriodKey = period.Code,
            NextValue = scheme.StartingValue,
            IssuedCount = 0,
        };
    }

    /// <summary>
    /// Decides the number a document gets, the counter to save, or the rules that refused.
    /// </summary>
    /// <param name="scheme">The tenant's rendering rule for this document type.</param>
    /// <param name="sequence">The counter row, read under a row lock by the caller.</param>
    /// <param name="request">What the document is asking for, including its allocation key.</param>
    public static NumberingDecision Allocate(
        DocumentNumberingScheme scheme,
        NumberingSequenceState sequence,
        NumberingAllocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<NumberingError>(NumberingSchemeRules.ValidateShape(scheme));

        if (!scheme.IsActive)
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.SchemeInactive,
                "This numbering scheme is switched off, so it cannot register documents."));
        }

        if (string.IsNullOrWhiteSpace(request.AllocationKey))
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.AllocationKeyEmpty,
                "Without an allocation key a retry cannot be told apart from a second document."));
        }

        if (sequence.DocumentType != scheme.DocumentType)
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.SequenceDocumentTypeMismatch,
                $"The counter row is for {sequence.DocumentType} but the scheme numbers {scheme.DocumentType}."));
        }

        NumberingPeriodKey? period = null;

        // Only derivable once the start month is usable; asking the fiscal calendar with a bad month
        // would throw instead of reporting the defect the caller can fix.
        if (scheme.FiscalStartMonth is >= 1 and <= 12)
        {
            period = NumberingPeriodKey.Of(request.On, scheme.PeriodComponent, scheme.FiscalStartMonth);

            if (!string.Equals(sequence.PeriodKey, period.Value.Code, StringComparison.Ordinal))
            {
                errors.Add(new NumberingError(
                    NumberingErrorCode.SequencePeriodMismatch,
                    $"The counter row covers period '{sequence.PeriodKey}' but this document date falls in '{period.Value.Code}'. Open the new period's counter instead of rolling this one over."));
            }
        }

        if (sequence.NextValue < scheme.StartingValue)
        {
            errors.Add(new NumberingError(
                NumberingErrorCode.CounterBelowStartingValue,
                $"The counter is at {sequence.NextValue.ToString(CultureInfo.InvariantCulture)} but this series started at {scheme.StartingValue.ToString(CultureInfo.InvariantCulture)}; a counter that has moved backwards would re-issue numbers already on paper."));
        }

        if (errors.Count > 0)
        {
            return Refused(errors);
        }

        // Checked before anything else that could consume a value, because a replay must never advance
        // the counter even when the rest of the row has since moved on.
        if (sequence.IssuedCount > 0
            && string.Equals(sequence.LastAllocationKey, request.AllocationKey, StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(sequence.LastAllocatedNumber))
            {
                return Refused([new NumberingError(
                    NumberingErrorCode.SequenceStateInconsistent,
                    "The counter remembers this allocation key but not the number it gave, so nothing safe can be returned. The row needs repairing, not re-running.")]);
            }

            return new NumberingDecision(
                NumberingDecisionStatus.Replayed,
                NoErrors,
                sequence.LastAllocatedNumber,
                sequence);
        }

        if (!NumberingFormat.Fits(scheme.Width, sequence.NextValue))
        {
            return Refused([new NumberingError(
                NumberingErrorCode.ValueOutgrowsPadding,
                $"The next value {sequence.NextValue.ToString(CultureInfo.InvariantCulture)} needs more than {scheme.Width.ToString(CultureInfo.InvariantCulture)} digits. Widen the scheme; do not truncate and do not let the length drift.")]);
        }

        if (request.ExpectedNextValue is { } expected && expected != sequence.NextValue)
        {
            return Refused([new NumberingError(
                NumberingErrorCode.StaleSequenceRead,
                $"The counter has moved from {expected.ToString(CultureInfo.InvariantCulture)} to {sequence.NextValue.ToString(CultureInfo.InvariantCulture)} since it was read. Re-read the row and retry; the allocation key makes that retry return the original number if this document already had one.")]);
        }

        var value = sequence.NextValue;
        var number = NumberingFormat.Render(scheme.Prefix, period!.Value, scheme.Width, value);

        var advanced = sequence with
        {
            NextValue = checked(value + 1),
            IssuedCount = sequence.IssuedCount + 1,
            LastAllocationKey = request.AllocationKey,
            LastAllocatedNumber = number,
        };

        return new NumberingDecision(NumberingDecisionStatus.Allocated, NoErrors, number, advanced);
    }

    private static NumberingDecision Refused(List<NumberingError> errors) =>
        new(NumberingDecisionStatus.Refused, errors);
}
