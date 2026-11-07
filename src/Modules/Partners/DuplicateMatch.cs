namespace Ostagar.Modules.Partners;

/// <summary>
/// One collision found by the duplicate rule: which existing partner, on which identifier, and how much that
/// identifier is worth.
/// </summary>
/// <remarks>
/// Reported per collision rather than as a bare boolean because the repair differs by kind. A matching e-TIN
/// means the record on screen is the same legal entity and the clerk is looking at a duplicate; a matching
/// phone number usually means two businesses share one agent's handset, and the answer is to add the missing
/// identifiers, not to abandon the record.
/// </remarks>
/// <param name="ExistingPartner">The partner already stored with that value.</param>
/// <param name="ExistingCode">Its code, for the review screen to link to.</param>
/// <param name="ExistingLegalName">Its legal name, for the review screen to read out.</param>
/// <param name="Kind">Which register both records claim to hold.</param>
/// <param name="Authority">How much that register proves: authoritative matches confirm, suggestive matches raise.</param>
/// <param name="Value">The colliding value, in the stored normalised form.</param>
public sealed record DuplicateMatch(
    PartnerId ExistingPartner,
    PartnerCode ExistingCode,
    PartnerName ExistingLegalName,
    PartnerIdentifierKind Kind,
    IdentifierAuthority Authority,
    string Value);
