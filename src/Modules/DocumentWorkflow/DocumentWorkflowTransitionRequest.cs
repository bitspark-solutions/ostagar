namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// What somebody is asking the machine to accept as a decision.
/// </summary>
/// <remarks>
/// <see cref="OccurredAt"/> is supplied by the caller and written straight onto the history entry. The
/// engine takes no clock, so a decision can be recorded, replayed and tested at a fixed instant — and a
/// transition made at 23:59 on the last day of a fiscal month lands where the paper says it did rather
/// than where the test suite happened to run.
///
/// <see cref="Reason"/> is demanded whenever the transition declares it must be
/// (<see cref="DocumentTransition.RequiresReason"/>), which is every send-back, rejection and cancellation:
/// the three moves a later reader cannot judge without an explanation.
///
/// <see cref="CorrectsEntryId"/> is set when this decision reverses an earlier one on the same document, so
/// the correction is linked rather than silently contradictory — and the earlier entry stays exactly as it
/// was, because a rewritten history is not a record of a decision but of a change of mind.
/// </remarks>
public sealed record DocumentWorkflowTransitionRequest
{
    /// <summary>Who is deciding. Required and checked, because an unattributed approval is worth nothing to an auditor.</summary>
    public required Guid ActorUserId { get; init; }

    /// <summary>The state being asked for.</summary>
    public required string ToStateCode { get; init; }

    /// <summary>When the decision was made, not when it was read.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>Why, when the transition demands one.</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>The recorded decision this one reverses.</summary>
    public Guid? CorrectsEntryId { get; init; }
}
