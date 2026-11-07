namespace Ostagar.Modules.Partners;

/// <summary>
/// Decides whether a proposed set of identifiers describes a partner that is already on file.
/// </summary>
/// <remarks>
/// <para>
/// A pure function over identifiers, with no repository and no name matching, and that is the substance of
/// the rule rather than a limitation of it. The legacy project let a typed name create master records and
/// then discovered, years later, that one buyer had four records because four clerks spelled it four ways —
/// an aged-receivables report that is silently wrong is worse than a loud error, because it is trusted.
/// </para>
///
/// <para>
/// So: match on identifiers only, compare them in their normalised form (<see cref="PartnerIdentifier.MatchKey"/>),
/// confirm only on an authoritative register, and treat a suggestive register as a question for a human. A
/// same-name partner with no shared identifier comes back <see cref="DuplicateVerdict.Unique"/>, which is
/// correct — <c>Rahima Garments</c> is a common trade name and two businesses may hold it.
/// </para>
///
/// <para>
/// The exclusion parameter matters as much as the matching: the same call is used when adding a
/// bank account to a partner that already exists, and a check that compares a partner with itself reports
/// every one of its own accounts as a duplicate of itself.
/// </para>
/// </remarks>
public static class DuplicateMatchEvaluator
{
    /// <summary>
    /// Runs the rule.
    /// </summary>
    /// <param name="proposed">Every identifier the new or updated partner presents, including the ones derived
    /// from its bank accounts and contact points.</param>
    /// <param name="existing">Stored identifiers of other partners, as returned by
    /// <see cref="IPartnerDuplicateDirectory"/>.</param>
    /// <param name="excludePartnerId">The partner being updated, so it cannot be its own duplicate.</param>
    public static DuplicateMatchReport Evaluate(
        IEnumerable<PartnerIdentifier> proposed,
        IEnumerable<ExistingPartnerIdentifier> existing,
        PartnerId? excludePartnerId = null)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentNullException.ThrowIfNull(existing);

        var candidates = (from identifier in proposed
                          group identifier by (identifier.Kind, identifier.MatchKey) into shared
                          select shared.First()).ToList();

        var matches = new List<DuplicateMatch>();

        foreach (var stored in existing)
        {
            ArgumentNullException.ThrowIfNull(stored);

            if (excludePartnerId is { } self && self == stored.PartnerId)
            {
                continue;
            }

            var collided = candidates.FirstOrDefault(candidate => candidate.Matches(stored.Identifier));

            if (collided is null)
            {
                continue;
            }

            matches.Add(new DuplicateMatch(
                stored.PartnerId,
                stored.Code,
                stored.LegalName,
                stored.Identifier.Kind,
                PartnerIdentifier.AuthorityOf(stored.Identifier.Kind),
                stored.Identifier.Value));
        }

        matches.Sort(Compare);

        var verdict = matches switch
        {
            { Count: 0 } => DuplicateVerdict.Unique,
            _ when matches.Any(match => match.Authority == IdentifierAuthority.Authoritative) => DuplicateVerdict.ConfirmedDuplicate,
            _ => DuplicateVerdict.SuspectedDuplicate,
        };

        return new DuplicateMatchReport(verdict, matches);
    }

    /// <summary>
    /// True when a match is strong enough to refuse the record outright. Stated as its own function because
    /// the caller's next question is always "can I save anyway", and the answer must not be re-derived
    /// differently in a second place.
    /// </summary>
    public static bool BlocksCreation(DuplicateMatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return report.Verdict == DuplicateVerdict.ConfirmedDuplicate;
    }

    private static int Compare(DuplicateMatch left, DuplicateMatch right)
    {
        var authority = left.Authority.CompareTo(right.Authority);

        if (authority != 0)
        {
            return authority;
        }

        var kind = string.CompareOrdinal(left.Kind.ToString(), right.Kind.ToString());

        if (kind != 0)
        {
            return kind;
        }

        return left.ExistingPartner.Value.CompareTo(right.ExistingPartner.Value);
    }
}
