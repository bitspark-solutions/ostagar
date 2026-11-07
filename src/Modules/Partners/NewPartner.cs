using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// Everything a partner is created with, in one deliberate act: an allocated code, an identity minted by the
/// registration path, at least one register-backed identifier, and the currencies it trades in.
/// </summary>
/// <remarks>
/// <para>
/// This type is the shape of the creation door, and the fields that are missing from it are the point. There
/// is no name-only constructor, no code that can be typed, no currency that defaults to the tenant's, and no
/// "created on" that reads a clock: <see cref="RegisteredAt"/> is supplied by the caller so the domain has one
/// time source and a test can pin it. Anything the caller has not decided stays undecided rather than being
/// filled in, because a defaulted master record is indistinguishable from a real one after a hundred rows.
/// </para>
///
/// <para>
/// Addresses, contact points and bank accounts are optional here and become mandatory at the moment they are
/// needed: a customer must have a registered address before it can be invoiced, and no partner can be paid
/// before it has a payment channel. Refusing at creation would block the small supplier a pilot tenant
/// genuinely has, and refusing at use is the honest version of the same rule.
/// </para>
/// </remarks>
public sealed record NewPartner
{
    public NewPartner(
        TenantId tenantId,
        PartnerId id,
        PartnerCode code,
        PartnerName legalName,
        PartnerIdentificationBasis basis,
        IReadOnlyList<PartnerIdentifier> identifiers,
        IReadOnlyList<CurrencyCode> documentCurrencies,
        DateTimeOffset registeredAt,
        PartnerName? tradingName = null,
        BanglaName? banglaName = null,
        IReadOnlyList<PartnerAddress>? addresses = null,
        IReadOnlyList<PartnerContactPoint>? contactPoints = null,
        IReadOnlyList<PartnerBankAccount>? bankAccounts = null)
    {
        ArgumentNullException.ThrowIfNull(identifiers);
        ArgumentNullException.ThrowIfNull(documentCurrencies);

        TenantId = tenantId;
        Id = id;
        Code = code;
        LegalName = legalName;
        Basis = basis;
        Identifiers = [.. identifiers];
        DocumentCurrencies = [.. documentCurrencies];
        RegisteredAt = registeredAt;
        TradingName = tradingName;
        BanglaName = banglaName;
        Addresses = [.. addresses ?? []];
        ContactPoints = [.. contactPoints ?? []];
        BankAccounts = [.. bankAccounts ?? []];
    }

    public TenantId TenantId { get; }

    /// <summary>The identity the registration path minted for this party. Nothing else may assign one.</summary>
    public PartnerId Id { get; }

    /// <summary>Allocated by <see cref="IPartnerCodeAllocator"/>, never typed.</summary>
    public PartnerCode Code { get; }

    public PartnerName LegalName { get; }

    public PartnerName? TradingName { get; }

    public BanglaName? BanglaName { get; }

    /// <summary>What the record's identity rests on: an authoritative register, or two independent suggestive ones.</summary>
    public PartnerIdentificationBasis Basis { get; }

    public IReadOnlyList<PartnerIdentifier> Identifiers { get; }

    /// <summary>The currencies this party's documents are raised in. At least one, and never inferred.</summary>
    public IReadOnlyList<CurrencyCode> DocumentCurrencies { get; }

    public IReadOnlyList<PartnerAddress> Addresses { get; }

    public IReadOnlyList<PartnerContactPoint> ContactPoints { get; }

    public IReadOnlyList<PartnerBankAccount> BankAccounts { get; }

    /// <summary>Supplied by the caller's clock. A domain aggregate that reads <c>DateTimeOffset.Now</c> cannot be tested at a fixed date.</summary>
    public DateTimeOffset RegisteredAt { get; }
}
