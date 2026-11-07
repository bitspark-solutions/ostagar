namespace Ostagar.Api.Controllers;

/// <summary>
/// Payload for writing and posting a manual journal in the caller's own company.
/// </summary>
/// <param name="JournalCode">The journal to number this entry in, by the code the company owns.</param>
/// <param name="PostingDate">The date the entry takes effect, which decides its period.</param>
/// <param name="Memo">What this entry is for, in the owner's own words.</param>
/// <param name="Lines">At least two lines, which must balance exactly.</param>
public sealed record CreateJournalRequest(
    string JournalCode,
    DateOnly PostingDate,
    string Memo,
    IReadOnlyList<JournalLineRequest> Lines);
