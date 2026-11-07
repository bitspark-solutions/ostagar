namespace Ostagar.Modules.Partners;

/// <summary>
/// How a partner can be reached. The set is what Bangladeshi garment trade actually uses, not a generic
/// phone/email pair.
/// </summary>
/// <remarks>
/// WhatsApp is a channel of record in this market: order confirmations, size approvals and dispatch
/// instructions routinely happen there rather than by e-mail, and a merchant's mobile is frequently the
/// only reachable number. A model with one "Phone" and one "Email" column cannot represent a factory whose
/// landline is the office, whose mobile is the merchandiser, and whose WhatsApp is the order sheet — and the
/// notifications agent needs the distinction to know which channel a message may be sent on.
/// </remarks>
public enum ContactKind
{
    /// <summary>Office landline.</summary>
    Landline,

    /// <summary>Mobile number, usually the merchandiser's.</summary>
    Mobile,

    /// <summary>E-mail address.</summary>
    Email,

    /// <summary>WhatsApp on a mobile number. A messaging channel, not a second phone field.</summary>
    WhatsApp,

    /// <summary>Facsimile, still on many factories' letterheads.</summary>
    Fax,

    /// <summary>Website.</summary>
    Website,
}
