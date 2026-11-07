using System.Globalization;

namespace Ostagar.Modules.Partners;

/// <summary>
/// Identity of one price list, so an order line can record which list its price came from.
/// </summary>
/// <remarks>
/// The reference matters because of the plan's rule that historical documents do not recompute when a rule
/// changes. A sales order that stores only a number cannot be audited once the list is revised; a sales order
/// that stores the list it was priced from can be shown beside the price that list carried on that date,
/// which is what settles a buyer's query about a two-year-old shipment.
/// </remarks>
public readonly record struct PriceListId
{
    private readonly Guid _value;

    private PriceListId(Guid value) => _value = value;

    public Guid Value => _value;

    public bool IsEmpty => _value == Guid.Empty;

    /// <summary>Minted only when a list is being issued, never handed back for something unstored.</summary>
    public static PriceListId New() => new(Guid.NewGuid());

    public static PriceListId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A price list id must not be empty.", nameof(value))
            : new PriceListId(value);

    public override string ToString() => _value.ToString("D", CultureInfo.InvariantCulture);
}
