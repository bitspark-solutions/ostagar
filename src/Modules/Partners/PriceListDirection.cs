namespace Ostagar.Modules.Partners;

/// <summary>
/// Whether a price list states what we charge this partner or what they charge us.
/// </summary>
/// <remarks>
/// Both exist for the same party in this trade: a trading house buys finished garments at our export price
/// and sells us the knitted fabric at its own. Deriving the direction from the partner's roles would break
/// exactly there — a dual-role partner has one record and two price directions — so the direction is a
/// property of the list, and the role capability is only checked to confirm the partner can be priced that
/// way at all.
/// </remarks>
public enum PriceListDirection
{
    /// <summary>Prices we quote the partner as a customer.</summary>
    Sales,

    /// <summary>Prices the partner quotes us as a vendor.</summary>
    Purchase,
}
