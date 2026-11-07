namespace Ostagar.Modules.Partners;

/// <summary>
/// The identifiers a Bangladeshi garment business is actually found holding, and the only kinds a partner
/// record accepts.
/// </summary>
/// <remarks>
/// This list is the duplicate-detection vocabulary, so it is deliberately the set a factory keeps on hand
/// rather than a general-purpose "document type" enum: the trade licence number from the BAITI/DPHT form,
/// the e-TIN the NBR issues, the VAT registration under the commercial entity, the Export Registration
/// Certificate (ERC) that every exporter must hold to ship, and then the contact-level values that are
/// unique most of the time.
///
/// <para><b>Authoritative in this product:</b> trade licence, e-TIN, VAT registration and ERC. Each is
/// issued to one business by an authority that does not knowingly issue twice, each is written on the
/// documents a factory already files, and each survives a change of name, address or phone number — which
/// is exactly the property a stable identifier needs.</para>
///
/// <para><b>Merely suggestive:</b> bank account number, phone and e-mail. A bank account is strong evidence
/// but is shared when a holding company collects for a subsidiary, changes when a factory moves banks, and
/// is the field most likely to be mistyped during a data load. A phone number is a sales agent's personal
/// line in a large fraction of Bangladeshi SMEs — the same handset serves four businesses — and an e-mail
/// address is frequently the owner's Gmail, reused across a family's firms. Blocking a new party on those
/// would be wrong often enough that people would stop trusting the check, and the check would then be
/// decoration.</para>
///
/// <para>A partner's <i>name</i> is on this list nowhere, and never will be. Names collide constantly
/// (<c>Rahima Garments</c> is one of the commonest trade names in the country) and a name match tells you
/// nothing about whether the two records are the same legal entity. Name matching is the mechanism that
/// produced the legacy duplicates in the first place, because it silently let them through.</para>
/// </remarks>
public enum PartnerIdentifierKind
{
    /// <summary>Trade licence number issued by the local authority (DPHT/BACSI number). Authoritative.</summary>
    TradeLicenceNumber,

    /// <summary>NBR e-TIN. Authoritative.</summary>
    TaxIdentificationNumber,

    /// <summary>Commercial tax / VAT registration number. Authoritative.</summary>
    VatRegistrationNumber,

    /// <summary>Export Registration Certificate (ERC) number from the Ministry of Commerce. Authoritative.</summary>
    ExportRegistrationCertificateNumber,

    /// <summary>A counted bank account number. Suggestive.</summary>
    BankAccountNumber,

    /// <summary>A phone or mobile number, normalised to digits. Suggestive.</summary>
    PhoneNumber,

    /// <summary>An e-mail address. Suggestive.</summary>
    EmailAddress,
}
