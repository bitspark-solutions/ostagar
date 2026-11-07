using System.Globalization;
using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// The price for one style on a partner's list, referenced by style id and held in the list's currency.
/// </summary>
/// <remarks>
/// <para>
/// The style is a <see cref="StyleId"/>, not a copy of the catalogue's name, fabric or size curve. A price
/// line that stores a style name starts disagreeing with the catalogue the moment the style is renamed, and
/// a buyer's quotation that quotes "Navy Polo" while the catalogue says "Navy Polo Shirt" is two products to
/// anyone reading both.
/// </para>
///
/// <para>
/// <see cref="UnitPrice"/> must be positive: a price of zero is not "free", it is a missing price, and the
/// legacy tree's habit of defaulting a blank price field to zero is how a shipment went out invoiced at
/// nothing. A genuinely free issue is a hundred percent discount, which is stated here as an explicit
/// commercial decision and nets to zero on its own line.
/// </para>
/// </remarks>
public sealed record PriceListLine
{
    public PriceListLine(StyleId style, Money unitPrice, decimal discountPercentage = 0m)
    {
        if (style.IsEmpty)
        {
            throw new ArgumentException("A price line must name a style.", nameof(style));
        }

        if (!unitPrice.IsPositive)
        {
            throw new ArgumentException(
                $"A price of {unitPrice} is not a price. Use a discount for something genuinely free of charge.",
                nameof(unitPrice));
        }

        if (discountPercentage is < 0m or > 100m)
        {
            throw new ArgumentException(
                $"'{discountPercentage.ToString(CultureInfo.InvariantCulture)}%' is outside the range a discount can take.",
                nameof(discountPercentage));
        }

        if (decimal.Round(discountPercentage, 4) != discountPercentage)
        {
            throw new ArgumentException(
                "A discount percentage finer than 0.0001% cannot be settled in any currency.",
                nameof(discountPercentage));
        }

        Style = style;
        UnitPrice = unitPrice;
        DiscountPercentage = discountPercentage;
    }

    /// <summary>The catalogued style this price is for. A reference only; the catalogue owns the description.</summary>
    public StyleId Style { get; }

    /// <summary>The list price before discount, in the list's currency.</summary>
    public Money UnitPrice { get; }

    /// <summary>Contractual discount off the list price, exact, 0 to 100 inclusive.</summary>
    public decimal DiscountPercentage { get; }

    /// <summary>The currency the line is priced in — always the list's own currency.</summary>
    public CurrencyCode Currency => UnitPrice.Currency.Code;

    /// <summary>The money taken off, computed at the currency's scale so the two parts re-add to the list price.</summary>
    public Money DiscountAmount() =>
        Money.Round(
            UnitPrice.Currency,
            UnitPrice.Amount * DiscountPercentage / 100m);

    /// <summary>
    /// What the partner actually pays per piece. Derived as list minus discount rather than as a percentage of
    /// the total, so a discounted line and its discount always sum back to the quoted price.
    /// </summary>
    public Money NetUnitPrice() => Money.Subtract(UnitPrice, DiscountAmount());
}
