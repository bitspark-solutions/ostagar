using System.Globalization;
using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// A partner's agreed prices for one direction and one currency, over a stated period.
/// </summary>
/// <remarks>
/// <para>
/// Three rules here come from how quotations actually go wrong in this trade:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>One currency, and a currency the partner's documents are traded in.</b> A USD price list against a
/// partner who is only ever invoiced in BDT is not a spare list, it is a pricing mistake waiting to be picked
/// up by a sales order; a quotation printed in the wrong currency has to be re-issued and the buyer notices.
/// The check is a refusal at issue time, named in the error.
/// </description></item>
/// <item><description>
/// <b>An expiry date is mandatory.</b> A price list that never expires is a price list nobody maintains, and
/// the factory will discover that in the worst way — by shipping at a 2024 rate that quietly lost them money
/// against 2026 fabric costs. An open-ended range is therefore not representable: <see cref="ValidTo"/> is a
/// required value and must fall strictly after <see cref="ValidFrom"/>.
/// </description></item>
/// <item><description>
/// <b>Two lists for one partner, one direction and one currency must not overlap.</b> When they do, the price
/// a document takes depends on which list the code happened to search first, and no screen says so. Refusing
/// the second list forces someone to close the first one, which is the decision the data needs anyway.
/// </description></item>
/// </list>
///
/// <para>
/// Every amount carries its <see cref="Currency"/> through <see cref="Money"/>, so a BDT list cannot contain
/// a taka figure written as a bare number and cannot be added to a USD line. Currency scale is the currency's
/// own (<see cref="Currency.MinorDigits"/>), which is what stops a JPY price acquiring imaginary decimals.
/// </para>
/// </remarks>
public sealed record PriceList
{
    private readonly List<PriceListLine> _lines;

    private PriceList(PriceListId id, PartnerId owner, PriceListDirection direction, Currency currency, DateOnly validFrom, DateOnly validTo, IEnumerable<PriceListLine> lines)
    {
        Id = id;
        Owner = owner;
        Direction = direction;
        Currency = currency;
        ValidFrom = validFrom;
        ValidTo = validTo;
        _lines = [.. lines];
    }

    /// <summary>The list's identity, which an order line references so a price can be traced back to its source.</summary>
    public PriceListId Id { get; }

    /// <summary>The partner the list belongs to. A list is always a partner's own, never a shared table.</summary>
    public PartnerId Owner { get; }

    public PriceListDirection Direction { get; }

    /// <summary>The single currency every line on this list is priced in.</summary>
    public Currency Currency { get; }

    public DateOnly ValidFrom { get; }

    public DateOnly ValidTo { get; }

    public IReadOnlyList<PriceListLine> Lines => _lines;

    /// <summary>
    /// Issues a list against a partner, enforcing the rules that only the partner can answer: the currency it
    /// is traded in, the role the direction requires, and whether the party is open for business at all.
    /// Reached through <see cref="Partner.IssuePriceList"/>, which is the only path that also attaches the list,
    /// so no caller can build a priced list that nothing owns.
    /// </summary>
    /// <exception cref="PartnerRuleException">The partner does not trade in that currency, cannot be priced that way, or is inactive.</exception>
    internal static PriceList IssuedFor(
        Partner partner,
        PriceListDirection direction,
        Currency currency,
        DateOnly validFrom,
        DateOnly validTo,
        IEnumerable<PriceListLine> lines,
        PriceListId? id = null)
    {
        ArgumentNullException.ThrowIfNull(partner);
        ArgumentNullException.ThrowIfNull(lines);

        if (!partner.IsActive)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PartnerInactive,
                $"{partner.Code} ({partner.LegalName}) is inactive, so a new price list would price documents for a party that cannot trade.");
        }

        var requiredRole = direction == PriceListDirection.Sales ? PartnerRole.Customer : PartnerRole.Vendor;

        if (!partner.HasRole(requiredRole))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.RoleNotHeld,
                $"A {direction} price list needs the {requiredRole} role, which {partner.Code} does not hold.");
        }

        if (!partner.TradesInCurrency(currency.Code))
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PriceListCurrencyNotTraded,
                $"{partner.Code} ({partner.LegalName}) is not traded in {currency.Code}; its documents are raised in "
                + $"{string.Join(", ", partner.DocumentCurrencies.Select(code => code.Value))}. "
                + "Issue the list in a traded currency or record the new currency on the partner first.");
        }

        if (validTo.DayNumber <= validFrom.DayNumber)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PriceListValidityInvalid,
                $"A price list valid from {validFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} to "
                + $"{validTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} has no days it is valid on. "
                + "Every list needs an expiry, because a list without one is never maintained.");
        }

        var materialised = new List<PriceListLine>();
        var seenStyles = new HashSet<StyleId>();

        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);

            if (line.Currency != currency.Code)
            {
                throw new PartnerRuleException(
                    PartnerErrorCode.PriceListLineCurrencyMismatch,
                    $"A line for style {line.Style} is priced in {line.Currency.Value} but the list is a {currency.Code} list. "
                    + "Prices are quoted in one currency per list; a second currency needs a second list so an order line cannot mix them.");
            }

            if (!seenStyles.Add(line.Style))
            {
                throw new PartnerRuleException(
                    PartnerErrorCode.PriceListStyleDuplicated,
                    $"Style {line.Style} is priced twice on this list. Which of the two prices an order takes would be decided by search order.");
            }

            materialised.Add(line);
        }

        if (materialised.Count == 0)
        {
            throw new PartnerRuleException(
                PartnerErrorCode.PriceListEmpty,
                "A price list with no lines is a blank that reads as an agreement.");
        }

        var list = new PriceList(id ?? PriceListId.New(), partner.Id, direction, currency, validFrom, validTo, materialised);

        partner.EnsureNoOverlappingList(list);

        return list;
    }

    /// <summary>
    /// Inclusive on both ends. A list stops being usable the day after its expiry, and an order dated the
    /// expiry day itself is still priced — the date the parties signed, not a tolerance around it.
    /// </summary>
    public bool IsEffectiveOn(DateOnly date) =>
        date.DayNumber >= ValidFrom.DayNumber && date.DayNumber <= ValidTo.DayNumber;

    /// <summary>The line for a style, or null when this list does not price it.</summary>
    public PriceListLine? LineFor(StyleId style) => _lines.FirstOrDefault(line => line.Style == style);

    /// <summary>True when this list's validity overlaps the proposed range, for the same direction and currency.</summary>
    public bool Overlaps(PriceList other) =>
        Direction == other.Direction
        && Currency.Code == other.Currency.Code
        && ValidFrom.DayNumber <= other.ValidTo.DayNumber
        && other.ValidFrom.DayNumber <= ValidTo.DayNumber;

    public override string ToString() =>
        $"{Direction} {Currency.Code} {ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.."
        + ValidTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
