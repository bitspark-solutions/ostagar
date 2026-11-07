namespace Ostagar.Modules.Partners;

/// <summary>
/// Asks whether a partner still has work in flight, which is what decides whether it can be deactivated or
/// have a role withdrawn.
/// </summary>
/// <remarks>
/// <para>
/// "Open" is not this module's definition to make. A sales order that has been dispatched but not invoiced, a
/// purchase order awaiting a GRN, an unpaid bill, a work order still on the lines, an allocation against a
/// receipt — each belongs to a different aggregate and only its owner can say whether it is finished. Partners
/// ask, and the answer is a yes or a no with a name attached; partners never keep a counter of other modules'
/// documents, because a counter maintained in two places is the plan's original sin.
/// </para>
///
/// <para>
/// The question exists because deactivating a partner under an open order is the orphaning defect: the order
/// still exists, still needs to be invoiced and dispatched, and now names a party that the product says is
/// closed. Either the order is refused at its own step with a confusing message, or it posts against a partner
/// nobody is maintaining. Refusing the deactivation while work is in flight is the only answer that leaves both
/// true.
/// </para>
///
/// <para>
/// <see cref="PartnerRole"/> is part of the question because the two sides are independent: a trading house can
/// have every invoice settled while a fabric order is still open, and withdrawing the customer role would be a
/// decision about a different set of documents.
/// </para>
/// </remarks>
public interface IOpenDocumentProbe
{
    /// <summary>
    /// True when the partner has at least one unfinished document for that role — or for either role, when
    /// <paramref name="role"/> is null.
    /// </summary>
    Task<bool> HasOpenDocumentsAsync(
        PartnerId partner,
        PartnerRole? role = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A short human name for the oldest open document, so the refusal can say what is holding the partner open
    /// instead of only that something is. Null when there is nothing open.
    /// </summary>
    Task<string?> FindOldestOpenDocumentAsync(
        PartnerId partner,
        PartnerRole? role = null,
        CancellationToken cancellationToken = default);
}
