namespace Ostagar.Modules.Partners;

/// <summary>
/// On what evidence this partner's identity rests. There is no option for "someone typed a name".
/// </summary>
/// <remarks>
/// The whole point of this enum is that it has exactly two values and neither of them can be satisfied by
/// free text. It exists because the pilot tenant's first data load contains two different populations:
/// registered factories and trading houses, which all have a trade licence or an e-TIN, and a long tail of
/// small accessory suppliers and transport contractors who genuinely do not. Forcing an authoritative
/// document on the second group would make the product unusable for exactly the small-factory band the plan
/// targets (§1.4); allowing name-only records for them would recreate the legacy defect.
///
/// So the informal tail is admitted on <see cref="SecondaryEvidence"/> — two independent suggestive
/// identifiers — and is permanently marked unverified, which is what stops it carrying a credit limit or
/// being paid without a bank account. A partner on secondary evidence is a partner the product admits it
/// does not know, and says so on screen.
/// </remarks>
public enum PartnerIdentificationBasis
{
    /// <summary>At least one authoritative identifier (trade licence, e-TIN, VAT registration, ERC) is recorded.</summary>
    AuthoritativeDocument,

    /// <summary>
    /// No authoritative document exists, so at least two suggestive identifiers are required and the
    /// partner stays flagged unverified: no credit limit, and payment only through a recorded bank account.
    /// </summary>
    SecondaryEvidence,
}
