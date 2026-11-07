namespace Ostagar.Modules.Partners;

/// <summary>
/// What a partner is <em>to us</em>, which is a capability on one identity rather than a kind of record.
/// </summary>
/// <remarks>
/// In garment trade a trading house that buys your output frequently also supplies the fabric it is made
/// from, and a wash plant you pay is also the party you send jobwork to. Modelling those as two record
/// types forces a second row for one real business, which splits its receivable and payable balances, and
/// then the aged-receivables report the owner relies on shows a partial figure with nothing on screen
/// saying so. Roles are therefore optional capabilities attached to a single
/// <see cref="Partner"/>; <see cref="Partner.Roles"/> lists whichever the party actually holds.
/// </remarks>
public enum PartnerRole
{
    /// <summary>We sell to this partner: sales orders, invoices, receipts, credit.</summary>
    Customer,

    /// <summary>We buy from this partner: purchase orders, bills, payments, advances.</summary>
    Vendor,
}
