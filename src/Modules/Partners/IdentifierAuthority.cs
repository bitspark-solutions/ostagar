namespace Ostagar.Modules.Partners;

/// <summary>
/// How much weight a matching identifier value carries when deciding whether two records are one business.
/// </summary>
/// <remarks>
/// Duplicate detection has to be honest about which of the numbers a factory actually keeps can bear the
/// weight of a hard refusal. Stating the difference in the type, instead of leaving it in a comment, means
/// the rule "confirm a duplicate only on an authoritative match" cannot be implemented by accident against
/// the wrong set of identifiers.
///
/// The consequence of getting this backwards is asymmetric, and that asymmetry is the whole design:
/// confirming on something merely suggestive blocks a legitimate new party and teaches users to work
/// around the check; accepting an authoritative mismatch silently corrupts an aged-receivables report,
/// which is a worse outcome than an error message because nobody ever sees it.
/// </remarks>
public enum IdentifierAuthority
{
    /// <summary>
    /// Issued to exactly one business by an authority that does not issue duplicates. A match refuses the
    /// record outright.
    /// </summary>
    Authoritative,

    /// <summary>
    /// Unique enough to be worth checking, wrong often enough that it must not block a real party. A match
    /// raises a review, naming the existing partner, and a person decides.
    /// </summary>
    Suggestive,
}
