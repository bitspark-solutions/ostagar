namespace Ostagar.Modules.Partners;

/// <summary>
/// The verdict of a duplicate check together with every collision that produced it.
/// </summary>
/// <remarks>
/// A single verdict with no detail is how a duplicate check becomes noise: the clerk is told "possible
/// duplicate" and cannot see which party, on which number, so the warning is clicked through and the next
/// record is opened under a spelling variation anyway. The match list is part of the answer for that reason,
/// and it is ordered so two runs on the same data show the same list in the same order.
///
/// The verdict is computed from the strongest match present, never from the count of matches — five
/// suggestive hits on a phone number shared by four concerns are still a review, and one e-TIN hit is always
/// a refusal.
/// </remarks>
public sealed record DuplicateMatchReport
{
    public DuplicateMatchReport(DuplicateVerdict verdict, IEnumerable<DuplicateMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        Verdict = verdict;
        Matches = [.. matches];
    }

    public DuplicateVerdict Verdict { get; }

    public IReadOnlyList<DuplicateMatch> Matches { get; }

    /// <summary>The partners that must be looked at before this record can be saved.</summary>
    public IReadOnlyList<PartnerId> ConflictingPartners =>
        [.. Matches.Select(match => match.ExistingPartner).Distinct()];
}
