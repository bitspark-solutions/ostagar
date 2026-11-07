namespace Ostagar.Modules.Partners;

/// <summary>
/// The shape of a payment term, as export garment factories actually agree them.
/// </summary>
/// <remarks>
/// Net-days covers local supply. The advance forms cover the part of this trade that a generic
/// "PaymentTerms = 'Net 30'" column cannot express: an export buyer normally pays a deposit against the
/// shipment — 30% at order confirmation, balance against a copy of the bill of lading — and a fabric or
/// trim supplier often demands full advance before releasing goods, which is the difference between a
/// usable supplier record and a supplier that cannot be used at all. Fixed-day-of-month covers the monthly
/// settlement a buying house proposes when it says "payment on the 5th of the following month".
/// </remarks>
public enum PaymentTermKind
{
    /// <summary>Due a whole number of days after the document date.</summary>
    NetDays,

    /// <summary>Due on a fixed day of the month following the document month.</summary>
    FixedDayOfMonth,

    /// <summary>Payable in full before delivery: one hundred percent advance.</summary>
    Advance,

    /// <summary>
    /// A stated percentage payable in advance, the remainder on a balance term. Splitting this from
    /// <see cref="Advance"/> rather than letting 100% mean advance keeps the arithmetic honest: a term that
    /// is fully advance has no balance to schedule.
    /// </summary>
    PartialAdvance,
}
