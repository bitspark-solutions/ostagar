namespace Ostagar.Modules.Partners;

/// <summary>
/// Which of a partner's addresses this is, because the one that receives an invoice is not the one the
/// goods go to and a factory rarely is either.
/// </summary>
/// <remarks>
/// Garment trade keeps these apart constantly: the registered office is where the trade licence was issued,
/// the factory is where the cutting happens, the bonded warehouse or clearing agent is where a shipment is
/// dispatched from, and the bank or head office is where the invoice must land for payment to be processed.
/// A single address column forces the clerk to pick one and the other documents are then wrong — which in
/// Bangladesh is not cosmetic, since a VAT invoice must show the address the registration was issued
/// against.
/// </remarks>
public enum AddressKind
{
    /// <summary>The address on the trade licence and VAT file. Required for a partner we invoice.</summary>
    Registered,

    /// <summary>Where an invoice or statement is sent.</summary>
    Invoice,

    /// <summary>Where goods are dispatched to or collected from.</summary>
    Delivery,

    /// <summary>The production unit, which is frequently not the registered office.</summary>
    Factory,
}
