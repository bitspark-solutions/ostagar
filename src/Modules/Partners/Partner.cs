using System.Globalization;
using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// One business this factory trades with, seen as a single identity that may hold two capabilities.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one aggregate and not a customer table plus a vendor table.</b> In garment trade the same party
/// very often buys the output and supplies the input: a trading house places the export order and also
/// releases the knit fabric, a wash plant is paid for jobwork and is bought thread from. Two records for one
/// real business is not untidiness — it splits the receivable and the payable across two ids, so the aged
/// report understates one side, the balance ageing hides the other, and nothing on screen tells the owner the
/// two lines are the same company they have been paying for a decade. Roles are therefore capabilities
/// (<see cref="CustomerProfile"/>, <see cref="VendorProfile"/>) hanging off one identity.
/// </para>
///
/// <para>
/// <b>Identity is stable and names are not.</b> <see cref="Id"/> and <see cref="Code"/> are set at
/// registration and there is no operation on this type that changes either. <see cref="ChangeLegalName"/>,
/// <see cref="ChangeTradingName"/> and <see cref="ChangeBanglaName"/> exist because a party's name changes
/// constantly in this market — a proprietorship becomes a limited company, "Exports" is added, a spelling on
/// the trade licence turns out to be wrong. The three names are three fields because they are three facts, and
/// the legal name on a licence, the name on a letterhead and the name in Bangla on a challan are not
/// interchangeable. A rename that leaves every document, ledger line and price list pointing at the same id
/// is the entire point.
/// </para>
///
/// <para>
/// <b>Nothing here is creatable from free text.</b> A code comes from <see cref="IPartnerCodeAllocator"/>, an
/// identifier from a named register and is validated into that register's form, a ledger account is a
/// reference to the chart with a declared side, a price line is a reference to a catalogued style id, and a
/// partner cannot exist with fewer identifiers than its declared evidence basis requires. Names are validated
/// as text and are never an identifier: nothing in this aggregate derives a code, an account or a duplicate
/// decision from one.
/// </para>
///
/// <para>
/// <b>Refusal is the default.</b> No payment channel, no payment; no credit limit, no credit sale; no
/// effective price list, no price; an open document, no deactivation. Where the legacy tree reached for a
/// default — a first row in a list, a zero balance, a generated code — this type stops and names the reason,
/// because a silent default in a ledger is indistinguishable from a decision and costs more to unwind.
/// </para>
/// </remarks>
public sealed class Partner : ITenantScoped
{
    private readonly List<PartnerIdentifier> _identifiers = [];
    private readonly List<PartnerAddress> _addresses = [];
    private readonly List<PartnerContactPoint> _contactPoints = [];
    private readonly List<PartnerBankAccount> _bankAccounts = [];
    private readonly List<CurrencyCode> _documentCurrencies = [];
    private readonly List<PriceList> _priceLists = [];

    private Partner(NewPartner draft)
    {
        TenantId = draft.TenantId;
        Id = draft.Id;
        Code = draft.Code;
        LegalName = draft.LegalName;
        TradingName = draft.TradingName;
        BanglaName = draft.BanglaName;
        Basis = draft.Basis;
        CreatedAt = draft.RegisteredAt;
        Status = PartnerStatus.Active;
        _identifiers.AddRange(draft.Identifiers);
        _addresses.AddRange(draft.Addresses);
        _contactPoints.AddRange(draft.ContactPoints);
        _bankAccounts.AddRange(draft.BankAccounts);
        _documentCurrencies.AddRange(draft.DocumentCurrencies);
    }

    /// <inheritdoc />
    public TenantId TenantId { get; set; }

    /// <summary>Minted once at registration. Nothing on this type changes it, and a rename cannot orphan a reference.</summary>
    public PartnerId Id { get; }

    /// <summary>Issued by the numbering service. Never derived from the name, never random, never reassigned.</summary>
    public PartnerCode Code { get; }

    public PartnerName LegalName { get; private set; }

    /// <summary>The name the party trades as, when it differs from the licence. Null when it does not.</summary>
    public PartnerName? TradingName { get; private set; }

    /// <summary>The party's name in Bangla script, held separately so a Latin legal name stays intact for banks and LCs.</summary>
    public BanglaName? BanglaName { get; private set; }

    /// <summary>The evidence the record was opened on. Fixed at registration; <see cref="IsIdentityVerified"/> reads the facts.</summary>
    public PartnerIdentificationBasis Basis { get; private set; }

    public PartnerStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>When the partner was closed off for new business. History is never removed, so this is the only trace of a deactivation.</summary>
    public DateTimeOffset? InactiveOn { get; private set; }

    public IReadOnlyList<PartnerIdentifier> Identifiers => _identifiers;

    public IReadOnlyList<PartnerAddress> Addresses => _addresses;

    public IReadOnlyList<PartnerContactPoint> ContactPoints => _contactPoints;

    public IReadOnlyList<PartnerBankAccount> BankAccounts => _bankAccounts;

    /// <summary>The currencies this party's documents may be raised in. At least one, always stated.</summary>
    public IReadOnlyList<CurrencyCode> DocumentCurrencies => _documentCurrencies;

    public IReadOnlyList<PriceList> PriceLists => _priceLists;

    /// <summary>The customer capability, or null when we do not sell to this party.</summary>
    public CustomerProfile? Customer { get; private set; }

    /// <summary>The vendor capability, or null when we do not buy from this party.</summary>
    public VendorProfile? Vendor { get; private set; }

    public bool IsActive => Status == PartnerStatus.Active;

    /// <summary>
    /// True once an authoritative register is on file, whatever the record was opened with. Derived from the
    /// identifiers rather than stored as a flag, so an unverified partner that later supplies a trade licence
    /// becomes verified by the fact of adding it and cannot stay mislabelled.
    /// </summary>
    public bool IsIdentityVerified => _identifiers.Any(identifier => identifier.Authority == IdentifierAuthority.Authoritative);

    /// <summary>Which capabilities this party holds, in a fixed order so a screen does not depend on insert order.</summary>
    public IReadOnlyList<PartnerRole> Roles
    {
        get
        {
            var roles = new List<PartnerRole>(2);

            if (Customer is not null)
            {
                roles.Add(PartnerRole.Customer);
            }

            if (Vendor is not null)
            {
                roles.Add(PartnerRole.Vendor);
            }

            return roles;
        }
    }

    /// <summary>
    /// Creates a partner. Every rule that decides whether a business may be opened on file at all is here, so
    /// there is exactly one door and it cannot be walked past.
    /// </summary>
    /// <exception cref="PartnerRuleException">
    /// The code looks derived from a name, the trading name repeats the legal name, the identifiers are
    /// insufficient for the declared evidence basis or repeat each other, or no document currency is stated.
    /// </exception>
    public static Partner Register(NewPartner draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.Id.IsEmpty)
        {
            throw new ArgumentException("A partner must be created with a minted identity.", nameof(draft));
        }

        if (draft.TenantId.IsEmpty)
        {
            throw new ArgumentException("A partner belongs to exactly one tenant; none was given.", nameof(draft));
        }

        if (draft.TradingName is { } trading && IsSameName(trading.Value, draft.LegalName.Value))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.TradingNameRedundant,
                $"The trading name repeats the legal name for {draft.Code}. Leave it unset; two copies of one fact drift apart later.");
        }

        EnsureCodeIsNotDerivedFromNames(draft.Code, draft.LegalName, draft.TradingName, draft.BanglaName);

        if (draft.DocumentCurrencies.Count == 0)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.DocumentCurrencyRequired,
                $"{draft.Code} has no document currency. A currency is not defaulted from the tenant's functional one — a party invoiced in taka and a party invoiced in dollars are different arrangements.");
        }

        if (draft.DocumentCurrencies.Distinct().Count() != draft.DocumentCurrencies.Count)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.DocumentCurrencyRequired,
                $"{draft.Code} lists the same document currency twice.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var identifier in draft.Identifiers)
        {
            ArgumentNullException.ThrowIfNull(identifier);

            if (!seen.Add(identifier.Kind + ":" + identifier.MatchKey))
            {
                throw new PartnerRuleException(
                    PartnerErrorCode.IdentifierDuplicated,
                    $"{draft.Code} holds {identifier.Kind} '{identifier.Value}' twice.");
            }
        }

        EnsureEvidenceSufficiency(draft.Code, draft.Basis, draft.Identifiers);

        var partner = new Partner(draft);

        partner.EnsureSinglePreferredChannel();

        return partner;
    }

    /// <summary>Grants the customer capability: a receivable account, and optionally a credit limit and a default term.</summary>
    /// <exception cref="PartnerRuleException">The role is already held, the accounts or currencies conflict, an invoice address is missing, or an unverified party is being given credit.</exception>
    public void GrantCustomerRole(CustomerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (Customer is not null)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.RoleAlreadyHeld,
                $"{Code} is already a customer. Replacing its terms is a separate, deliberate act, not a side effect of granting the role again.");
        }

        if (_addresses.All(address => address.Kind != AddressKind.Registered))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.RegisteredAddressRequired,
                $"{Code} cannot be invoiced: no registered address is on file, and a VAT invoice has to show the address the registration was issued against.");
        }

        if (profile.CreditLimit is { } limit)
        {
            if (!IsIdentityVerified)
            {
                throw new PartnerRuleException(
                    PartnerErrorCode.UnverifiedIdentityCannotHoldCredit,
                    $"{Code} holds no authoritative identifier, so no credit limit may be granted against it. Record the trade licence or e-TIN first.");
            }

            if (!TradesInCurrency(limit.Currency.Code))
            {
                throw new PartnerRuleException(
                    PartnerErrorCode.CreditLimitCurrencyNotTraded,
                    $"A credit limit in {limit.Currency.Code} cannot be set for {Code}, whose documents are in "
                    + $"{string.Join(", ", _documentCurrencies.Select(code => code.Value))}.");
            }
        }

        if (Vendor is { } vendor && string.Equals(
                vendor.PayableAccount.Code,
                profile.ReceivableAccount.Code,
                StringComparison.Ordinal))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.TradeAccountConflict,
                $"'{profile.ReceivableAccount.Code}' cannot be both the receivable and the payable for {Code}. "
                + "One account for both sides nets what we are owed against what we owe, and both figures become wrong.");
        }

        Customer = profile;
    }

    /// <summary>Grants the vendor capability: a payable account and optionally a default term for what we owe.</summary>
    public void GrantVendorRole(VendorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (Vendor is not null)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.RoleAlreadyHeld,
                $"{Code} is already a vendor.");
        }

        if (Customer is { } customer && string.Equals(
                customer.ReceivableAccount.Code,
                profile.PayableAccount.Code,
                StringComparison.Ordinal))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.TradeAccountConflict,
                $"'{profile.PayableAccount.Code}' cannot be both the payable and the receivable for {Code}.");
        }

        Vendor = profile;
    }

    /// <summary>
    /// Withdraws a capability. Refuses while that side of the relationship still has work in flight, because a
    /// role removed under an open document leaves the document naming a party the product no longer treats as
    /// trading.
    /// </summary>
    /// <remarks>
    /// Price lists are left in place. A withdrawn customer role does not retroactively un-agree a price that
    /// was quoted and shipped against, and deleting the lists would destroy the only record of what the parties
    /// had settled on. Re-granting the role restores pricing; issuing a <em>new</em> list requires the role, so
    /// the capability still gates the future and not the past.
    /// </remarks>
    /// <param name="role">Which capability to withdraw.</param>
    /// <param name="hasOpenDocuments">Answered by <see cref="IOpenDocumentProbe"/>; this module cannot know it and does not guess.</param>
    /// <param name="openDocument">The document holding it open, for the message.</param>
    public void WithdrawRole(PartnerRole role, bool hasOpenDocuments, string? openDocument = null)
    {
        if (!HasRole(role))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.RoleNotHeld,
                $"{Code} does not hold the {role} role, so there is nothing to withdraw.");
        }

        if (hasOpenDocuments)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.OpenDocumentsExist,
                $"{Code} still has open {role} documents{(openDocument is { Length: > 0 } named ? $" (oldest: {named})" : string.Empty)}. "
                + "Close or reverse them before withdrawing the role, or the documents will reference a party that is no longer tradeable.");
        }

        if (role == PartnerRole.Customer)
        {
            Customer = null;
            return;
        }

        Vendor = null;
    }

    /// <summary>True when this party holds the capability, which is what a document checks before pricing or posting.</summary>
    public bool HasRole(PartnerRole role) => role switch
    {
        PartnerRole.Customer => Customer is not null,
        PartnerRole.Vendor => Vendor is not null,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown partner role."),
    };

    /// <summary>
    /// The ledger account this partner's balance sits on for that role. The one place a dual-role party cannot
    /// be answered wrongly: each role resolves to its own side, and the two codes are guaranteed distinct.
    /// </summary>
    public LedgerAccountReference AccountFor(PartnerRole role) => role switch
    {
        PartnerRole.Customer => Customer?.ReceivableAccount
            ?? throw new PartnerRuleException(PartnerErrorCode.RoleNotHeld, $"{Code} is not a customer, so it has no receivable account."),
        PartnerRole.Vendor => Vendor?.PayableAccount
            ?? throw new PartnerRuleException(PartnerErrorCode.RoleNotHeld, $"{Code} is not a vendor, so it has no payable account."),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown partner role."),
    };

    /// <summary>Renames the party. The identity and the code are untouched, and every document and price list keeps pointing at the same partner.</summary>
    public void ChangeLegalName(PartnerName legalName)
    {
        if (TradingName is { } trading && IsSameName(trading.Value, legalName.Value))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.TradingNameRedundant,
                $"The trading name already says '{legalName}'. Rename the trading name too, or unset it.");
        }

        EnsureCodeIsNotDerivedFromNames(Code, legalName, TradingName, BanglaName);

        LegalName = legalName;
    }

    /// <summary>Sets or clears the name the party trades as. Null means it trades under its legal name.</summary>
    public void ChangeTradingName(PartnerName? tradingName)
    {
        if (tradingName is { } trading && IsSameName(trading.Value, LegalName.Value))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.TradingNameRedundant,
                $"The trading name repeats the legal name for {Code}; unset it instead of storing it twice.");
        }

        EnsureCodeIsNotDerivedFromNames(Code, LegalName, tradingName, BanglaName);

        TradingName = tradingName;
    }

    /// <summary>Sets or clears the Bangla form of the name. A transliteration is refused by <see cref="BanglaName"/>.</summary>
    public void ChangeBanglaName(BanglaName? banglaName)
    {
        EnsureCodeIsNotDerivedFromNames(Code, LegalName, TradingName, banglaName);

        BanglaName = banglaName;
    }

    /// <summary>
    /// Records an identifier. Adding one to an existing partner is a duplicate-checking act as well as a data
    /// entry one, so the caller runs <see cref="DuplicateMatchEvaluator"/> over
    /// <see cref="IdentificationForMatching"/> first; this method enforces only what the aggregate can see.
    /// </summary>
    public void AddIdentifier(PartnerIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        RequireActiveForNewBusiness();

        if (_identifiers.Any(existing => existing.Matches(identifier)))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.IdentifierDuplicated,
                $"{Code} already holds {identifier.Kind} '{identifier.Value}'.");
        }

        _identifiers.Add(identifier);
    }

    /// <summary>
    /// Removes an identifier. Refused when doing so would drop the record below the evidence it was opened on,
    /// which would leave a partner that claims an authoritative basis and holds no authoritative document — or
    /// claims secondary evidence on one phone number.
    /// </summary>
    public void RemoveIdentifier(PartnerIdentifier identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        var stored = _identifiers.FirstOrDefault(existing => existing.Matches(identifier))
            ?? throw new PartnerRuleException(
                PartnerErrorCode.IdentifierNotHeld,
                $"{Code} does not hold {identifier.Kind} '{identifier.Value}'.");

        if (Basis == PartnerIdentificationBasis.AuthoritativeDocument
            && stored.Authority == IdentifierAuthority.Authoritative
            && _identifiers.Count(existing => existing.Authority == IdentifierAuthority.Authoritative) == 1)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.AuthoritativeIdentifierRequired,
                $"'{stored.Value}' is the only authoritative identifier on {Code}. Removing it would leave a record "
                + "whose identity rests on a typed name.");
        }

        if (Basis == PartnerIdentificationBasis.SecondaryEvidence
            && stored.Authority == IdentifierAuthority.Suggestive
            && _identifiers.Count(existing => existing.Authority == IdentifierAuthority.Suggestive && !existing.Matches(stored)) < 2)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.IdentificationEvidenceInsufficient,
                $"{Code} holds no authoritative identifier, so it cannot be left with fewer than two independent suggestive ones.");
        }

        _identifiers.Remove(stored);
    }

    /// <summary>Adds an address. Only one address may claim to be the registered or the invoicing one.</summary>
    public void AddAddress(PartnerAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        RequireActiveForNewBusiness();

        if (address.Kind is AddressKind.Registered or AddressKind.Invoice
            && _addresses.Any(existing => existing.Kind == address.Kind))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.AddressClaimDuplicated,
                $"{Code} already has a {address.Kind} address. A document would otherwise have to choose between two claims about the same fact.");
        }

        _addresses.Add(address);
    }

    /// <summary>Adds a contact channel. Only one per kind may be marked primary.</summary>
    public void AddContactPoint(PartnerContactPoint contactPoint)
    {
        ArgumentNullException.ThrowIfNull(contactPoint);

        RequireActiveForNewBusiness();

        if (contactPoint.IsPrimary
            && _contactPoints.Any(existing => existing.Kind == contactPoint.Kind && existing.IsPrimary))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelAmbiguous,
                $"{Code} already has a primary {contactPoint.Kind}. Name the one to change rather than adding a second.");
        }

        _contactPoints.Add(contactPoint);
    }

    /// <summary>Records a bank account. The account number is also an identifier, so it joins the duplicate set the moment it is stored.</summary>
    public void AddBankAccount(PartnerBankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        RequireActiveForNewBusiness();

        if (_bankAccounts.Any(existing => existing.AsIdentifier().Matches(account.AsIdentifier())))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.IdentifierDuplicated,
                $"{Code} already holds the account {account.AccountNumber}.");
        }

        if (!TradesInCurrency(account.Currency.Code))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentCurrencyNotTraded,
                $"{Code}'s documents are raised in {string.Join(", ", _documentCurrencies.Select(code => code.Value))}, "
                + $"so a {account.Currency.Code.Value} account has no document to settle. Record the currency on the partner first if this party really is paid in it.");
        }

        if (account.IsPreferred && _bankAccounts.Any(existing => !existing.IsArchived && existing.IsPreferred))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelAmbiguous,
                $"{Code} already has a designated account. Move the designation instead of adding a second one.");
        }

        _bankAccounts.Add(account);
    }

    /// <summary>
    /// Marks one account as the one to pay. The only way a preferred channel changes: an existing flag is moved
    /// by this named act rather than by whichever account was added last.
    /// </summary>
    public void DesignatePaymentChannel(PartnerBankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_bankAccounts.Contains(account))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelMissing,
                $"{account.AccountNumber} is not an account of {Code}.");
        }

        if (account.IsArchived)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelMissing,
                $"{account.AccountNumber} is archived, so money must not be sent to it.");
        }

        foreach (var existing in _bankAccounts)
        {
            existing.IsPreferred = ReferenceEquals(existing, account);
        }
    }

    /// <summary>Retires an account without deleting it, because documents already paid against it must still read correctly.</summary>
    public void ArchiveBankAccount(PartnerBankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_bankAccounts.Contains(account))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelMissing,
                $"{account.AccountNumber} is not an account of {Code}.");
        }

        account.Archive();
    }

    /// <summary>Brings a retired account back. Requires a deliberate call; nothing reinstates an account by accident.</summary>
    public void ReinstateBankAccount(PartnerBankAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (!_bankAccounts.Contains(account))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelMissing,
                $"{account.AccountNumber} is not an account of {Code}.");
        }

        account.Reinstate();
        EnsureSinglePreferredChannel();
    }

    /// <summary>
    /// The account money may move through, or a refusal naming the reason. Never a default: no channel is
    /// returned because it was first in a list.
    /// </summary>
    /// <param name="currency">When given, the channel must settle in that currency and the partner must trade in it.</param>
    /// <exception cref="PartnerRuleException">No usable channel exists, several are usable and none is designated, or the requested currency is not one this party trades in.</exception>
    public PartnerBankAccount RequirePaymentChannel(CurrencyCode? currency = null)
    {
        if (currency is { } requested && !TradesInCurrency(requested))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentCurrencyNotTraded,
                $"{Code} is not traded in {requested.Value}; its documents are raised in "
                + $"{string.Join(", ", _documentCurrencies.Select(code => code.Value))}.");
        }

        var usable = currency is { } wanted
            ? _bankAccounts.Where(account => !account.IsArchived && account.Currency.Code == wanted).ToList()
            : _bankAccounts.Where(account => !account.IsArchived).ToList();

        if (usable.Count == 0)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelMissing,
                currency is { } requestedCurrency
                    ? $"{Code} has no {requestedCurrency.Value} bank account. Payment is refused rather than routed to an account nobody recorded."
                    : $"{Code} has no bank account, so there is no channel to pay through. Record one; do not settle this against cash or an unrecorded account.");
        }

        var designated = usable.Where(account => account.IsPreferred).ToList();

        if (designated.Count > 1)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelAmbiguous,
                $"{Code} has {designated.Count} accounts marked as the one to pay. Designate one.");
        }

        if (designated.Count == 1)
        {
            return designated[0];
        }

        if (usable.Count == 1)
        {
            return usable[0];
        }

        throw new PartnerRuleException(
            PartnerErrorCode.PaymentChannelAmbiguous,
            $"{Code} has {usable.Count} accounts and none is designated. Choosing by list order would send money to an account nobody selected.");
    }

    /// <summary>True when at least one unarchived account exists — the question a payment screen asks before it offers to pay.</summary>
    public bool HasPaymentChannel => _bankAccounts.Any(account => !account.IsArchived);

    /// <summary>True when the party's documents are raised in that currency. Prices, credit limits and payments are all checked against this.</summary>
    public bool TradesInCurrency(CurrencyCode currency) =>
        _documentCurrencies.Any(traded => traded == currency);

    /// <summary>
    /// Replaces the traded-in currency set, refusing to withdraw a currency an effective price list still
    /// quotes in and refusing an empty set.
    /// </summary>
    /// <param name="currencies">The new set, stated in full.</param>
    /// <param name="asOf">The date against which price lists are judged, supplied by the caller's clock.</param>
    public void SetDocumentCurrencies(IReadOnlyList<CurrencyCode> currencies, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(currencies);

        RequireActiveForNewBusiness();

        if (currencies.Count == 0)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.DocumentCurrencyRequired,
                $"{Code} cannot be left with no document currency; a party with no currency can have no documents.");
        }

        if (currencies.Distinct().Count() != currencies.Count)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.DocumentCurrencyRequired,
                $"{Code} lists the same currency twice.");
        }

        foreach (var withdrawn in _documentCurrencies.Except(currencies))
        {
            var stillQuoting = _priceLists.Any(list => list.Currency.Code == withdrawn && list.IsEffectiveOn(asOf));

            if (stillQuoting)
            {
                throw new PartnerRuleException(
                    PartnerErrorCode.DocumentCurrencyInUse,
                    $"{Code}'s prices are still quoted in {withdrawn.Value} on an effective list. Withdraw the list first.");
            }
        }

        _documentCurrencies.Clear();
        _documentCurrencies.AddRange(currencies);
    }

    /// <summary>
    /// Issues and attaches a price list for this partner. The rules that need the partner — traded currency,
    /// matching role, active status, no overlapping list — are enforced here, in one call.
    /// </summary>
    public PriceList IssuePriceList(
        PriceListDirection direction,
        Currency currency,
        DateOnly validFrom,
        DateOnly validTo,
        IEnumerable<PriceListLine> lines,
        PriceListId? id = null)
    {
        var list = PriceList.IssuedFor(this, direction, currency, validFrom, validTo, lines, id);

        _priceLists.Add(list);

        return list;
    }

    /// <summary>
    /// The agreed price for a style on a date, or a refusal that says which condition is missing. A document
    /// that cannot find a price here has no price, and inventing one is the failure this whole path exists to
    /// prevent.
    /// </summary>
    /// <remarks>
    /// The role is checked as well as the list. A price list survives a withdrawn capability because it is a
    /// record of what was agreed, and an old document may still need to cite it — but a <em>new</em> quotation
    /// against a party that is no longer a customer would price a sale to someone the product no longer sells
    /// to, so the capability is what gates the future and the list is not.
    /// </remarks>
    public PriceListLine RequirePrice(
        StyleId style,
        PriceListDirection direction,
        CurrencyCode currency,
        DateOnly date)
    {
        RequireActiveForNewBusiness();

        var requiredRole = direction == PriceListDirection.Sales ? PartnerRole.Customer : PartnerRole.Vendor;

        if (!HasRole(requiredRole))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.RoleNotHeld,
                $"{Code} is not a {requiredRole}, so no {direction} price may be quoted to it now.");
        }

        var effective = _priceLists
            .Where(list => list.Direction == direction && list.Currency.Code == currency && list.IsEffectiveOn(date))
            .ToList();

        if (effective.Count == 0)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PriceListExpired,
                $"{Code} has no {direction} price list in {currency.Value} effective on "
                + $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}. A price is quoted from an agreement, not from a blank.");
        }

        if (effective.Count > 1)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PriceListOverlap,
                $"{Code} has {effective.Count} overlapping {direction} lists in {currency.Value}; which price applies is decided by search order, so it is refused instead.");
        }

        return effective[0].LineFor(style)
            ?? throw new PartnerRuleException(
                PartnerErrorCode.StyleNotPriced,
                $"Style {style} is not priced on {Code}'s {direction} list {effective[0]}. Add it to the list or quote a different style.");
    }

    /// <summary>
    /// The credit check for this partner as a customer. Exposure comes from the receivables projection, so the
    /// figure compared against the limit is the one the owner sees on the ageing report.
    /// </summary>
    public CreditDecision EvaluateCredit(Money currentExposure, Money proposedOrder)
    {
        if (Customer is not { } customer)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.RoleNotHeld,
                $"{Code} is not a customer, so there is no credit limit to check against.");
        }

        return CreditLimitRule.Evaluate(customer.CreditLimit, currentExposure, proposedOrder);
    }

    /// <summary>
    /// Closes the partner to new business. Refuses while documents are in flight; never deletes.
    /// </summary>
    /// <param name="hasOpenDocuments">Answered by <see cref="IOpenDocumentProbe"/>.</param>
    /// <param name="when">The caller's clock.</param>
    /// <param name="openDocument">The oldest open document, for the message.</param>
    public void Deactivate(bool hasOpenDocuments, DateTimeOffset when, string? openDocument = null)
    {
        if (Status == PartnerStatus.Inactive)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PartnerAlreadyInactive,
                $"{Code} is already inactive, so nothing would change. Reporting a change that did not happen is a false success.");
        }

        if (hasOpenDocuments)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.OpenDocumentsExist,
                $"{Code} has documents still open{(openDocument is { Length: > 0 } named ? $" (oldest: {named})" : string.Empty)}. "
                + "Deactivating now would leave them referencing a party the product treats as closed.");
        }

        Status = PartnerStatus.Inactive;
        InactiveOn = when;
    }

    /// <summary>
    /// Reopens the partner for new business. Its history, code and identifiers were never lost, so there is
    /// nothing to restore; <see cref="InactiveOn"/> is cleared because it records the closure that has now been
    /// lifted, and the audit trail is where the date of this decision lives.
    /// </summary>
    public void Reactivate()
    {
        if (Status == PartnerStatus.Active)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PartnerAlreadyActive,
                $"{Code} is already active, so nothing would change.");
        }

        Status = PartnerStatus.Active;
        InactiveOn = null;
    }

    /// <summary>Refuses new documents, quotations and identifiers against an inactive partner.</summary>
    /// <remarks>
    /// Settlements of documents that already exist are deliberately not covered by this: deactivating a supplier
    /// must not make an unpaid bill impossible to pay, and the rule that stops new business is what carries the
    /// owner's intention.
    /// </remarks>
    public void RequireActiveForNewBusiness()
    {
        if (!IsActive)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PartnerInactive,
                $"{Code} ({LegalName}) is{(InactiveOn is { } closed ? $" inactive since {closed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}" : string.Empty)} "
                + "and cannot carry new business.");
        }
    }

    /// <summary>
    /// Every identifier this partner presents for duplicate detection: the declared ones, plus the bank account
    /// numbers and the phone and e-mail contact points. A partner is matched by what it holds, not by what
    /// someone typed into a "check for duplicates" box, which is why an account added last month still guards
    /// against a record opened today.
    /// </summary>
    public IReadOnlyList<PartnerIdentifier> IdentificationForMatching()
    {
        var all = new List<PartnerIdentifier>(_identifiers);

        foreach (var account in _bankAccounts)
        {
            all.Add(account.AsIdentifier());
        }

        foreach (var contact in _contactPoints)
        {
            if (contact.AsIdentifier() is { } identifier)
            {
                all.Add(identifier);
            }
        }

        var distinct = new List<PartnerIdentifier>(all.Count);

        foreach (var candidate in all)
        {
            if (!distinct.Any(existing => existing.Matches(candidate)))
            {
                distinct.Add(candidate);
            }
        }

        return distinct;
    }

    /// <summary>True when the party trades under a name in Bangla script as well as a Latin one.</summary>
    public bool HasBanglaName => BanglaName is not null;

    internal void EnsureNoOverlappingList(PriceList proposed)
    {
        var clash = _priceLists.FirstOrDefault(existing => existing.Overlaps(proposed));

        if (clash is not null)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PriceListOverlap,
                $"{Code} already has a {clash.Direction} list in {clash.Currency.Code.Value} covering {clash} for style pricing. "
                + "Close or shorten that list first; two effective lists would make the price a function of which one is found first.");
        }
    }

    private void EnsureSinglePreferredChannel()
    {
        var preferred = _bankAccounts.Count(account => !account.IsArchived && account.IsPreferred);

        if (preferred > 1)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PaymentChannelAmbiguous,
                $"{Code} has {preferred} accounts marked as the one to pay.");
        }
    }

    private static void EnsureEvidenceSufficiency(
        PartnerCode code,
        PartnerIdentificationBasis basis,
        IReadOnlyList<PartnerIdentifier> identifiers)
    {
        var authoritative = identifiers.Count(identifier => identifier.Authority == IdentifierAuthority.Authoritative);

        var suggestive = identifiers
            .Where(identifier => identifier.Authority == IdentifierAuthority.Suggestive)
            .Select(identifier => identifier.Kind)
            .Distinct()
            .Count();

        if (basis == PartnerIdentificationBasis.AuthoritativeDocument && authoritative == 0)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.IdentificationEvidenceInsufficient,
                $"{code} claims an authoritative basis but holds no trade licence, e-TIN, VAT registration or ERC number. "
                + "A partner cannot be opened on a name.");
        }

        if (basis == PartnerIdentificationBasis.SecondaryEvidence && suggestive < 2)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.IdentificationEvidenceInsufficient,
                $"{code} holds no authoritative identifier, so it needs two independent suggestive ones (bank account, phone, e-mail) "
                + "to be opened at all. One is not enough to tell two businesses apart, and a name is not an identifier.");
        }
    }

    /// <summary>
    /// Refuses a code whose letters are the party's own name with digits on the end.
    /// </summary>
    /// <remarks>
    /// A tripwire, not the guarantee — the guarantee is that <see cref="PartnerCode"/> can only come from
    /// <see cref="IPartnerCodeAllocator"/> and must contain sequence digits. This catches the habit the legacy
    /// tree was built on, where a clerk typed the name and the system mangled it into a code: five or more
    /// letters shared with the front of a name is a name, not a sequence. The threshold is deliberately high,
    /// because the ordinary role prefixes an allocator emits (<c>CUS</c>, <c>VEN</c>, <c>PRT</c>) must not be
    /// mistaken for a party called "Venus Garments".
    /// </remarks>
    private static void EnsureCodeIsNotDerivedFromNames(
        PartnerCode code,
        PartnerName legalName,
        PartnerName? tradingName,
        BanglaName? banglaName)
    {
        const int SharedLettersThatProveAName = 5;

        var codeLetters = code.Letters;

        if (codeLetters.Length < SharedLettersThatProveAName)
        {
            return;
        }

        var names = new List<string> { legalName.Value };

        if (tradingName is { } trading)
        {
            names.Add(trading.Value);
        }

        if (banglaName is { } bangla)
        {
            names.Add(bangla.Value);
        }

        foreach (var name in names)
        {
            var nameLetters = PartnerTextRules.LettersOnly(name);

            var sharesTheName = nameLetters.Length >= SharedLettersThatProveAName
                && (nameLetters.StartsWith(codeLetters, StringComparison.Ordinal)
                    || codeLetters.StartsWith(nameLetters, StringComparison.Ordinal));

            if (sharesTheName)
            {
                throw new PartnerRuleException(
                    PartnerErrorCode.CodeDerivedFromName,
                    $"'{code}' is the name '{name}' with digits on the end. Codes are allocated from a sequence, "
                    + "because a code derived from a name breaks the day the party is renamed and collides the day two parties sound alike.");
            }
        }
    }

    private static bool IsSameName(string left, string right) =>
        string.Equals(
            PartnerTextRules.AlphaNumericOnly(left),
            PartnerTextRules.AlphaNumericOnly(right),
            StringComparison.Ordinal);
}
