namespace Ostagar.Modules.Partners;

/// <summary>
/// The English and Bangla label for every piece of partner vocabulary a screen can show.
/// </summary>
/// <remarks>
/// <para>
/// Law L17 makes Bangla a first-class UI language rather than a retrofit, and the precedent is the ledger's
/// <c>BaseChartOfAccounts</c>, which carries <c>Name</c> and <c>NameBangla</c> on every account. A party's
/// role, status, payment term and identifier kind are shown to the owner as often as an account name is, and
/// if the domain does not state their Bangla form the frontend invents one per screen — which is how one enum
/// ends up with three spellings and a search that misses two of them.
/// </para>
///
/// <para>
/// <b>The Bangla wording needs a native review before the first pilot.</b> It is written here to make L17 true
/// from the start, but a bookkeeping or trade term in Bangla has conventional wording a non-native speaker
/// gets subtly wrong, and a subtly wrong label on an owner's screen is worse than an English one they can
/// read. Tracked with the P9 Bangla assets, exactly as the chart of accounts is.
/// </para>
///
/// <para>
/// Labels are for display only. Nothing parses them, and no rule keys on wording — a change of phrase must
/// never change behaviour, which is the failure mode of the legacy tree's name-sniffing classifiers.
/// </para>
/// </remarks>
public static class PartnerVocabulary
{
    /// <summary>What the party is to us.</summary>
    public static LocalizedLabel Label(PartnerRole role) => role switch
    {
        PartnerRole.Customer => new LocalizedLabel("Customer", "ক্রেতা"),
        PartnerRole.Vendor => new LocalizedLabel("Vendor", "সরবরাহকারী"),
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown partner role."),
    };

    /// <summary>Whether the party is open for new business.</summary>
    public static LocalizedLabel Label(PartnerStatus status) => status switch
    {
        PartnerStatus.Active => new LocalizedLabel("Active", "সক্রিয়"),
        PartnerStatus.Inactive => new LocalizedLabel("Inactive", "নিষ্ক্রিয়"),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown partner status."),
    };

    /// <summary>Which register an identifier came from.</summary>
    public static LocalizedLabel Label(PartnerIdentifierKind kind) => kind switch
    {
        PartnerIdentifierKind.TradeLicenceNumber => new LocalizedLabel("Trade licence number", "ট্রেড লাইসেন্স নম্বর"),
        PartnerIdentifierKind.TaxIdentificationNumber => new LocalizedLabel("e-TIN", "ই-টিন"),
        PartnerIdentifierKind.VatRegistrationNumber => new LocalizedLabel("VAT registration number", "ভ্যাট নিবন্ধন নম্বর"),
        PartnerIdentifierKind.ExportRegistrationCertificateNumber => new LocalizedLabel("Export registration certificate (ERC)", "রপ্তানি নিবন্ধন সনদ (ERC)"),
        PartnerIdentifierKind.BankAccountNumber => new LocalizedLabel("Bank account number", "ব্যাংক হিসাব নম্বর"),
        PartnerIdentifierKind.PhoneNumber => new LocalizedLabel("Phone number", "ফোন নম্বর"),
        PartnerIdentifierKind.EmailAddress => new LocalizedLabel("E-mail address", "ইমেইল ঠিকানা"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown identifier kind."),
    };

    /// <summary>Which of the party's places an address is.</summary>
    public static LocalizedLabel Label(AddressKind kind) => kind switch
    {
        AddressKind.Registered => new LocalizedLabel("Registered address", "নিবন্ধিত ঠিকানা"),
        AddressKind.Invoice => new LocalizedLabel("Invoice address", "ইনভয়েস ঠিকানা"),
        AddressKind.Delivery => new LocalizedLabel("Delivery address", "ডেলিভারি ঠিকানা"),
        AddressKind.Factory => new LocalizedLabel("Factory", "কারখানা"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown address kind."),
    };

    /// <summary>How the party can be reached.</summary>
    public static LocalizedLabel Label(ContactKind kind) => kind switch
    {
        ContactKind.Landline => new LocalizedLabel("Landline", "ল্যান্ডলাইন"),
        ContactKind.Mobile => new LocalizedLabel("Mobile", "মোবাইল"),
        ContactKind.Email => new LocalizedLabel("E-mail", "ইমেইল"),
        ContactKind.WhatsApp => new LocalizedLabel("WhatsApp", "হোয়াটসঅ্যাপ"),
        ContactKind.Fax => new LocalizedLabel("Fax", "ফ্যাক্স"),
        ContactKind.Website => new LocalizedLabel("Website", "ওয়েবসাইট"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown contact kind."),
    };

    /// <summary>When the money falls due.</summary>
    public static LocalizedLabel Label(PaymentTermKind kind) => kind switch
    {
        PaymentTermKind.NetDays => new LocalizedLabel("Net days", "নিট দিন"),
        PaymentTermKind.FixedDayOfMonth => new LocalizedLabel("Fixed day of month", "মাসের নির্দিষ্ট তারিখ"),
        PaymentTermKind.Advance => new LocalizedLabel("Full advance", "সম্পূর্ণ অগ্রিম"),
        PaymentTermKind.PartialAdvance => new LocalizedLabel("Partial advance", "আংশিক অগ্রিম"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown payment term kind."),
    };

    /// <summary>Whose prices a list states.</summary>
    public static LocalizedLabel Label(PriceListDirection direction) => direction switch
    {
        PriceListDirection.Sales => new LocalizedLabel("Selling price list", "বিক্রয় মূল্যতালিকা"),
        PriceListDirection.Purchase => new LocalizedLabel("Purchase price list", "ক্রয় মূল্যতালিকা"),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown price list direction."),
    };

    /// <summary>What evidence the record was opened on.</summary>
    public static LocalizedLabel Label(PartnerIdentificationBasis basis) => basis switch
    {
        PartnerIdentificationBasis.AuthoritativeDocument => new LocalizedLabel("Identified by official document", "আইনত স্বীকৃত নথি দ্বারা সনাক্ত"),
        PartnerIdentificationBasis.SecondaryEvidence => new LocalizedLabel("Identified by secondary evidence", "গৌণ প্রমাণ দ্বারা সনাক্ত"),
        _ => throw new ArgumentOutOfRangeException(nameof(basis), basis, "Unknown identification basis."),
    };

    /// <summary>Why a duplicate check ended the way it did.</summary>
    public static LocalizedLabel Label(DuplicateVerdict verdict) => verdict switch
    {
        DuplicateVerdict.Unique => new LocalizedLabel("No existing match", "আগের কোনো মিল নেই"),
        DuplicateVerdict.ConfirmedDuplicate => new LocalizedLabel("Same party already on file", "এই প্রতিষ্ঠানের নথি আগে থেকেই আছে"),
        DuplicateVerdict.SuspectedDuplicate => new LocalizedLabel("Possible duplicate — check before saving", "সম্ভাব্য পুনরাবৃত্তি — সংরক্ষণের আগে দেখুন"),
        _ => throw new ArgumentOutOfRangeException(nameof(verdict), verdict, "Unknown duplicate verdict."),
    };

    /// <summary>How much a matching identifier proves.</summary>
    public static LocalizedLabel Label(IdentifierAuthority authority) => authority switch
    {
        IdentifierAuthority.Authoritative => new LocalizedLabel("Official identifier", "সরকারি নম্বর"),
        IdentifierAuthority.Suggestive => new LocalizedLabel("Supporting identifier", "সহায়ক নম্বর"),
        _ => throw new ArgumentOutOfRangeException(nameof(authority), authority, "Unknown identifier authority."),
    };

    /// <summary>How a credit check ended.</summary>
    public static LocalizedLabel Label(CreditDecisionStatus status) => status switch
    {
        CreditDecisionStatus.Approved => new LocalizedLabel("Within credit limit", "ক্রহী সীমার মধ্যে"),
        CreditDecisionStatus.OverLimit => new LocalizedLabel("Over credit limit", "ক্রহী সীমা অতিক্রম"),
        CreditDecisionStatus.CreditLimitNotSet => new LocalizedLabel("No credit limit granted", "ক্রহী সীমা নির্ধারিত নেই"),
        CreditDecisionStatus.CurrencyMismatch => new LocalizedLabel("Limit and balance are in different currencies", "সীমা ও বকেয়া ভিন্ন মুদ্রায়"),
        CreditDecisionStatus.OrderValueInvalid => new LocalizedLabel("Order value is not a credit sale", "আদেশের মূল্য ক্রেডিট বিক্রয় নয়"),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown credit decision status."),
    };

    /// <summary>
    /// The sentence a rule refusal turns into. The code is what a client asserts on and what the log carries;
    /// this is what the owner's screen shows beside it.
    /// </summary>
    public static LocalizedLabel Label(PartnerErrorCode code) => code switch
    {
        PartnerErrorCode.Unspecified => new LocalizedLabel("The operation was refused", "কার্যটি বাতিল হয়েছে"),
        PartnerErrorCode.PartnerInactive => new LocalizedLabel("This party is inactive", "এই প্রতিষ্ঠান নিষ্ক্রিয়"),
        PartnerErrorCode.PartnerAlreadyInactive => new LocalizedLabel("This party is already inactive", "এই প্রতিষ্ঠান আগে থেকেই নিষ্ক্রিয়"),
        PartnerErrorCode.PartnerAlreadyActive => new LocalizedLabel("This party is already active", "এই প্রতিষ্ঠান আগে থেকেই সক্রিয়"),
        PartnerErrorCode.OpenDocumentsExist => new LocalizedLabel("Open documents still reference this party", "এই প্রতিষ্ঠানের ওপর বাকি নথি আছে"),
        PartnerErrorCode.RoleNotHeld => new LocalizedLabel("This party does not hold that role", "এই প্রতিষ্ঠান ওই ভূমিকায় নেই"),
        PartnerErrorCode.RoleAlreadyHeld => new LocalizedLabel("This role is already granted", "এই ভূমিকা আগে থেকেই দেওয়া আছে"),
        PartnerErrorCode.CodeDerivedFromName => new LocalizedLabel("The code looks taken from the name", "কোডটি নাম থেকে নেওয়া মনে হচ্ছে"),
        PartnerErrorCode.IdentificationEvidenceInsufficient => new LocalizedLabel("Not enough identifiers to open this party", "এই প্রতিষ্ঠান খুলতে যথেষ্ট পরিচয় নম্বর নেই"),
        PartnerErrorCode.AuthoritativeIdentifierRequired => new LocalizedLabel("An official identifier is required", "একটি সরকারি নম্বর প্রয়োজন"),
        PartnerErrorCode.UnverifiedIdentityCannotHoldCredit => new LocalizedLabel("An unverified party cannot be given credit", "অস্বীকৃত প্রতিষ্ঠানকে ধার দেওয়া যাবে না"),
        PartnerErrorCode.IdentifierDuplicated => new LocalizedLabel("This identifier is already recorded", "এই নম্বর আগে থেকেই লেখা আছে"),
        PartnerErrorCode.IdentifierNotHeld => new LocalizedLabel("This identifier is not recorded", "এই নম্বর লেখা নেই"),
        PartnerErrorCode.DuplicateConfirmed => new LocalizedLabel("The same party is already on file", "একই প্রতিষ্ঠানের নথি আগে থেকেই আছে"),
        PartnerErrorCode.DuplicateSuspected => new LocalizedLabel("A similar party may already be on file", "অনুরূপ প্রতিষ্ঠান আগে থেকেই থাকতে পারে"),
        PartnerErrorCode.DocumentCurrencyRequired => new LocalizedLabel("Choose the currencies this party trades in", "এই প্রতিষ্ঠানের লেনদেনের মুদ্রা নির্ধারণ করুন"),
        PartnerErrorCode.DocumentCurrencyInUse => new LocalizedLabel("A price list still uses this currency", "একটি মূল্যতালিকা এই মুদ্রা ব্যবহার করছে"),
        PartnerErrorCode.RegisteredAddressRequired => new LocalizedLabel("A registered address is required", "নিবন্ধিত ঠিকানা প্রয়োজন"),
        PartnerErrorCode.PaymentChannelMissing => new LocalizedLabel("No bank account is recorded for this party", "এই প্রতিষ্ঠানের ব্যাংক হিসাব লেখা নেই"),
        PartnerErrorCode.PaymentChannelAmbiguous => new LocalizedLabel("More than one bank account could be used", "একাধিক ব্যাংক হিসাব ব্যবহারযোগ্য"),
        PartnerErrorCode.PaymentCurrencyNotTraded => new LocalizedLabel("This party is not traded in that currency", "এই প্রতিষ্ঠান ওই মুদ্রায় লেনদেন করে না"),
        PartnerErrorCode.PriceListCurrencyNotTraded => new LocalizedLabel("This party is not traded in that currency", "এই প্রতিষ্ঠান ওই মুদ্রায় লেনদেন করে না"),
        PartnerErrorCode.PriceListLineCurrencyMismatch => new LocalizedLabel("The price is in a different currency from its list", "মূল্য তালিকার মুদ্রা থেকে ভিন্ন"),
        PartnerErrorCode.PriceListValidityInvalid => new LocalizedLabel("The price list is valid on no date", "মূল্যতালিকা কোনো তারিখে কার্যকর নয়"),
        PartnerErrorCode.PriceListEmpty => new LocalizedLabel("The price list has no lines", "মূল্যতালিকায় কোনো সারি নেই"),
        PartnerErrorCode.PriceListStyleDuplicated => new LocalizedLabel("The style is priced twice on this list", "এই তালিকায় স্টাইল দুবার মূল্যায়িত"),
        PartnerErrorCode.PriceListOverlap => new LocalizedLabel("Another price list already covers these dates", "আর একটি মূল্যতালিকা এই সময়কাল ঢেকে রাখছে"),
        PartnerErrorCode.PriceListExpired => new LocalizedLabel("No agreed price list covers this date", "এই তারিখের জন্য কোনো মূল্যতালিকা নেই"),
        PartnerErrorCode.StyleNotPriced => new LocalizedLabel("This style is not on the party's price list", "এই স্টাইল প্রতিষ্ঠানের মূল্যতালিকায় নেই"),
        PartnerErrorCode.CreditLimitNotSet => new LocalizedLabel("No credit limit has been granted", "ক্রহী সীমা নির্ধারিত হয়নি"),
        PartnerErrorCode.CreditCurrencyMismatch => new LocalizedLabel("The limit and the balance are in different currencies", "সীমা ও বকেয়া ভিন্ন মুদ্রায়"),
        PartnerErrorCode.CreditOrderValueInvalid => new LocalizedLabel("The order value is not a credit sale", "আদেশের মূল্য ক্রেডিট বিক্রয় নয়"),
        PartnerErrorCode.OverCreditLimit => new LocalizedLabel("The order exceeds the credit limit", "আদেশ ক্রহী সীমা অতিক্রম করছে"),
        PartnerErrorCode.TradingNameRedundant => new LocalizedLabel("The trading name repeats the legal name", "ব্যবসার নাম আইনি নামের সঙ্গে এক"),
        PartnerErrorCode.CreditLimitCurrencyNotTraded => new LocalizedLabel("The credit limit is in a currency this party is not traded in", "ক্রহী সীমা এই প্রতিষ্ঠানের মুদ্রা নয়"),
        PartnerErrorCode.TradeAccountConflict => new LocalizedLabel("One ledger account cannot be both receivable and payable", "একটি খতিয়ান হিসাব পাওনা ও দেনা দুটিই হতে পারে না"),
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown partner error code."),
    };
}
