namespace Ostagar.Api.Controllers;

/// <summary>
/// Payload for correcting a posted entry by posting its mirror.
/// </summary>
/// <param name="ReversalDate">The date the correction takes effect. It is deliberately the caller's to
/// choose, because reversing into a closed period is refused and the honest answer is to correct in the
/// period that is currently open.</param>
/// <param name="Memo">Why the original was wrong, which is the half an auditor cannot reconstruct from
/// the two entries alone.</param>
public sealed record ReverseJournalRequest(DateOnly ReversalDate, string Memo);
