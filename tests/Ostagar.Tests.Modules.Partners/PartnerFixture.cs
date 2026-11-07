using Ostagar.Core;
using Ostagar.Modules.Partners;

namespace Ostagar.Tests.Modules.Partners;

/// <summary>
/// Builds the partner shapes the rules are tested against, so a test states the one fact it is about and
/// nothing else. The dates and the tenant are pinned rather than read from a clock, because a rule test that
/// depends on today is a test that changes meaning overnight.
/// </summary>
internal static class PartnerFixture
{
    internal static readonly TenantId Tenant = TenantId.From(new Guid("6f1d0a3e-2c1b-4f57-9a08-7d2e5c1b9a11"));

    internal static readonly Currency Bdt = Currency.Of(CurrencyCode.Bdt);

    internal static readonly Currency Usd = Currency.Of(CurrencyCode.Usd);

    internal static readonly DateOnly Today = new(2026, 10, 16);

    internal static readonly DateTimeOffset RegisteredOn = new(2026, 10, 16, 9, 0, 0, TimeSpan.FromHours(6));

    /// <summary>
    /// A registered factory or trading house: an authoritative basis, a trade licence and an e-TIN, a
    /// registered address, and taka documents.
    /// </summary>
    internal static Partner TradeLicencePartner(
        string code = "PRT-1001",
        string legalName = "Sonali Cotton Mills Limited",
        IReadOnlyList<CurrencyCode>? currencies = null)
    {
        return Partner.Register(new NewPartner(
            Tenant,
            PartnerId.New(),
            PartnerCode.From(code),
            PartnerName.From(legalName, "Legal name"),
            PartnerIdentificationBasis.AuthoritativeDocument,
            [
                new PartnerIdentifier(PartnerIdentifierKind.TradeLicenceNumber, "RL/CTK/2019/00456"),
                new PartnerIdentifier(PartnerIdentifierKind.TaxIdentificationNumber, "4561237890"),
            ],
            currencies ?? [CurrencyCode.Bdt],
            RegisteredOn,
            addresses:
            [
                new PartnerAddress(
                    AddressKind.Registered,
                    "House 42, Road 7, Dhanmondi",
                    "Dhaka",
                    district: "Dhaka",
                    postCode: "1209"),
            ]));
    }

    /// <summary>
    /// A party opened on two suggestive identifiers because it has no trade licence — the small supplier the
    /// pilot tenant genuinely has, and the reason the evidence basis is a declared choice rather than a flag.
    /// </summary>
    internal static Partner SecondaryEvidencePartner(
        string code = "PRT-2001",
        string legalName = "Rahima Traders",
        IReadOnlyList<CurrencyCode>? currencies = null)
    {
        return Partner.Register(new NewPartner(
            Tenant,
            PartnerId.New(),
            PartnerCode.From(code),
            PartnerName.From(legalName, "Legal name"),
            PartnerIdentificationBasis.SecondaryEvidence,
            [
                new PartnerIdentifier(PartnerIdentifierKind.PhoneNumber, "01711 123456"),
                new PartnerIdentifier(PartnerIdentifierKind.EmailAddress, "rahima.traders@gmail.com"),
            ],
            currencies ?? [CurrencyCode.Bdt],
            RegisteredOn,
            addresses:
            [
                new PartnerAddress(AddressKind.Registered, "Shop 12, Baitul Aman", "Dhaka"),
            ]));
    }

    internal static CustomerProfile CustomerTerms(string receivableCode = "1030", Money? creditLimit = null, PaymentTerm? term = null) =>
        new(LedgerAccountReference.ForReceivable(receivableCode), creditLimit, term);

    internal static VendorProfile VendorTerms(string payableCode = "2010", PaymentTerm? term = null) =>
        new(LedgerAccountReference.ForPayable(payableCode), term);

    internal static PartnerBankAccount Account(
        string accountNumber,
        Currency? currency = null,
        bool isPreferred = false,
        string bankName = "Sonali Bank PLC",
        string accountName = "Sonali Cotton Mills Limited") =>
        new(
            bankName,
            accountName,
            accountNumber,
            currency ?? Bdt,
            branch: "Dhanmondi Branch",
            isPreferred: isPreferred);

    internal static StyleId NewStyle() => StyleId.From(Guid.NewGuid());

    internal static PriceListLine Line(StyleId style, decimal amount, Currency? currency = null, decimal discount = 0m) =>
        new(style, new Money(currency ?? Bdt, amount), discount);

    internal static ExistingPartnerIdentifier Stored(
        PartnerId partner,
        string code,
        string legalName,
        PartnerIdentifierKind kind,
        string value) =>
        new(partner, PartnerCode.From(code), PartnerName.From(legalName, "Legal name"), new PartnerIdentifier(kind, value));
}
