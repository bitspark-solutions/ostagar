namespace Ostagar.Modules.Partners;

/// <summary>
/// Machine-readable reasons a partner rule refused, so a client can translate and a test can assert.
/// </summary>
/// <remarks>
/// Codes rather than messages, following the ledger's <c>PostingErrorCode</c>. The legacy services returned
/// free text, which meant a caller could not tell one refusal from another without string matching, and the
/// text differed per call site — so the same defect announced itself three ways and was fixed in one.
///
/// Each code below is a refusal that has a specific repair. A UI that shows only "invalid data" cannot be
/// acted on by a clerk, and in this product the clerk is usually the owner's accounts person working from a
/// paper file.
/// </remarks>
public enum PartnerErrorCode
{
    /// <summary>A refusal that has not been given a code. Never returned by a rule; a placeholder for exception plumbing.</summary>
    Unspecified,

    /// <summary>The partner is inactive and cannot carry new business.</summary>
    PartnerInactive,

    /// <summary>The partner is already inactive, so deactivating it again would claim a change that did not happen.</summary>
    PartnerAlreadyInactive,

    /// <summary>The partner is not inactive, so reactivating it would claim a change that did not happen.</summary>
    PartnerAlreadyActive,

    /// <summary>
    /// A document that is not finished still references this partner, so deactivating or removing the role
    /// would orphan it. The repair is to close or reverse the document first.
    /// </summary>
    OpenDocumentsExist,

    /// <summary>The partner does not hold the role the operation needs.</summary>
    RoleNotHeld,

    /// <summary>The partner already holds that role; a second grant would silently replace terms someone set.</summary>
    RoleAlreadyHeld,

    /// <summary>The code looks like the party's name with digits appended, which is a code derived from a name.</summary>
    CodeDerivedFromName,

    /// <summary>
    /// Not enough identifiers of the kind the declared evidence basis requires. This is the rule that makes a
    /// name-only partner impossible to create.
    /// </summary>
    IdentificationEvidenceInsufficient,

    /// <summary>The partner's last authoritative identifier is being removed, which would downgrade its evidence.</summary>
    AuthoritativeIdentifierRequired,

    /// <summary>
    /// A partner admitted on secondary evidence is not trusted with credit. Granting the limit would turn an
    /// unverified identity into an exposure.
    /// </summary>
    UnverifiedIdentityCannotHoldCredit,

    /// <summary>An identifier of the same kind and value is already on this partner.</summary>
    IdentifierDuplicated,

    /// <summary>The partner does not hold the identifier being removed, so the removal would report a change that did not happen.</summary>
    IdentifierNotHeld,

    /// <summary>Two partners hold the same authoritative identifier: one business, two records. Refused outright.</summary>
    DuplicateConfirmed,

    /// <summary>Two partners share a suggestive identifier. Needs a person to decide, and cannot be resolved by a default.</summary>
    DuplicateSuspected,

    /// <summary>No document currency is recorded, so no document could ever be raised in a currency the partner trades in.</summary>
    DocumentCurrencyRequired,

    /// <summary>A currency in use by an existing price list cannot be withdrawn.</summary>
    DocumentCurrencyInUse,

    /// <summary>A registered address is required before the partner can be invoiced.</summary>
    RegisteredAddressRequired,

    /// <summary>
    /// A second address claiming to be the registered or invoicing one. A document would otherwise have to
    /// choose between two claims about the same fact, and the choice would be invisible on the print-out.
    /// </summary>
    AddressClaimDuplicated,

    /// <summary>A second contact channel of one kind marked primary, for the same reason as an account.</summary>
    PrimaryContactDuplicated,

    /// <summary>
    /// The partner has no bank account, so there is no channel to pay or be paid through. Refused rather than
    /// defaulted to cash or to an unrecorded account.
    /// </summary>
    PaymentChannelMissing,

    /// <summary>
    /// Several accounts are available and none is marked as the one to use. Choosing by list order would move
    /// money to an account nobody selected, so a person has to.
    /// </summary>
    PaymentChannelAmbiguous,

    /// <summary>Payment is proposed in a currency the partner's documents are not traded in.</summary>
    PaymentCurrencyNotTraded,

    /// <summary>The currency of a price list is not one the partner trades in.</summary>
    PriceListCurrencyNotTraded,

    /// <summary>A price list line is in a different currency from its list.</summary>
    PriceListLineCurrencyMismatch,

    /// <summary>A price list's expiry is not after its start, so it is valid on no date.</summary>
    PriceListValidityInvalid,

    /// <summary>A price list has no lines.</summary>
    PriceListEmpty,

    /// <summary>The same style is priced twice on one list, leaving the price a function of search order.</summary>
    PriceListStyleDuplicated,

    /// <summary>A list for the same partner, direction and currency already covers these dates.</summary>
    PriceListOverlap,

    /// <summary>No price list covers the requested date, so the document has no agreed price.</summary>
    PriceListExpired,

    /// <summary>The style is not on the partner's list, so its price was never agreed.</summary>
    StyleNotPriced,

    /// <summary>
    /// The customer has no credit limit. Refused: an absent decision is not an unlimited one.
    /// </summary>
    CreditLimitNotSet,

    /// <summary>The exposure and the limit are in different currencies, so the comparison is meaningless.</summary>
    CreditCurrencyMismatch,

    /// <summary>An order value of zero or less is not a credit sale; a negative value is a refund, not an order.</summary>
    CreditOrderValueInvalid,

    /// <summary>The committed exposure would exceed the limit. The overage is reported exactly, never clamped.</summary>
    OverCreditLimit,

    /// <summary>The credit limit is in a currency the partner's documents are not traded in.</summary>
    CreditLimitCurrencyNotTraded,

    /// <summary>
    /// One chart account has been named as both the receivable and the payable. Balances would net against
    /// each other and both the "what is owed to me" and "what I owe" figures would be wrong at once.
    /// </summary>
    TradeAccountConflict,

    /// <summary>A trading name identical to the legal name is stored nowhere; two copies of one fact disagree later.</summary>
    TradingNameRedundant,
}
