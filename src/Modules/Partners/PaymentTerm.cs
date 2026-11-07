using System.Globalization;
using Ostagar.Core;

namespace Ostagar.Modules.Partners;

/// <summary>
/// A payment term, validated into one of four shapes, that can state when money is due and how much of an
/// order value falls due now.
/// </summary>
/// <remarks>
/// <para>
/// The advance split is computed here rather than in a report because it is where the exactness has to live.
/// A 30% deposit on an order of BDT 1,234,567.89 is BDT 370,370.37 and the balance is the difference — not
/// a second, independent percentage of the total, which would leave a paisa unassigned and a document whose
/// two halves do not add up to its own total. The rounding decision is made once, named, and the balance is
/// derived from it.
/// </para>
///
/// <para>
/// No value here is defaulted. A term that does not match its kind — a net-days term with no days, a partial
/// advance with no balance schedule, an advance of 100% stored as a percentage instead of a kind — is
/// refused at construction, because a half-filled term row is what later produces a due date nobody can
/// explain.
/// </para>
/// </remarks>
public sealed record PaymentTerm
{
    private const int MaxNetDays = 365;

    /// <summary>The largest advance percentage this product can compute exactly, in minor units.</summary>
    private const decimal MaxPercentageScale = 0.0001m;

    private PaymentTerm(PaymentTermKind kind, int? netDays, int? dayOfMonth, decimal? advancePercentage, PaymentTerm? balance)
    {
        Kind = kind;
        NetDays = netDays;
        DayOfMonth = dayOfMonth;
        AdvancePercentage = advancePercentage;
        Balance = balance;
    }

    public PaymentTermKind Kind { get; }

    /// <summary>Set only for <see cref="PaymentTermKind.NetDays"/>.</summary>
    public int? NetDays { get; }

    /// <summary>Set only for <see cref="PaymentTermKind.FixedDayOfMonth"/>.</summary>
    public int? DayOfMonth { get; }

    /// <summary>Set only for <see cref="PaymentTermKind.PartialAdvance"/>; <see cref="PaymentTermKind.Advance"/> is one hundred percent by definition.</summary>
    public decimal? AdvancePercentage { get; }

    /// <summary>When the remainder of a partially advanced order falls due. Null when there is no remainder.</summary>
    public PaymentTerm? Balance { get; }

    /// <summary>Due a whole number of days after the document date. Days are exact and must be at least one.</summary>
    public static PaymentTerm ForNetDays(int days)
    {
        if (days is < 1 or > MaxNetDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(days),
                days,
                $"A net-days term is between 1 and {MaxNetDays} days; a longer arrangement is a loan, not a payment term.");
        }

        return new PaymentTerm(PaymentTermKind.NetDays, days, null, null, null);
    }

    /// <summary>
    /// Due on <paramref name="dayOfMonth"/> of the month following the document month — the monthly
    /// settlement a buying house offers.
    /// </summary>
    public static PaymentTerm ForFixedDayOfMonth(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dayOfMonth),
                dayOfMonth,
                "A fixed payment day must be a day of a month, 1 to 31.");
        }

        return new PaymentTerm(PaymentTermKind.FixedDayOfMonth, null, dayOfMonth, null, null);
    }

    /// <summary>Paid in full before delivery. Carries no percentage field, because there is no remainder to divide.</summary>
    public static PaymentTerm ForAdvance() => new(PaymentTermKind.Advance, null, null, null, null);

    /// <summary>
    /// A deposit of <paramref name="advancePercentage"/> percent, the balance on
    /// <paramref name="balanceTerm"/>. The percentage is a decimal, not money, and is bounded to a scale this
    /// product can settle.
    /// </summary>
    public static PaymentTerm ForPartialAdvance(decimal advancePercentage, PaymentTerm balanceTerm)
    {
        ArgumentNullException.ThrowIfNull(balanceTerm);

        if (advancePercentage <= 0m)
        {
            throw new ArgumentException(
                $"An advance of {advancePercentage.ToString(CultureInfo.InvariantCulture)}% is not an advance; use a net-days or fixed-day term.",
                nameof(advancePercentage));
        }

        if (advancePercentage >= 100m)
        {
            throw new ArgumentException(
                $"An advance of {advancePercentage.ToString(CultureInfo.InvariantCulture)}% leaves no balance to schedule; use {nameof(ForAdvance)}.",
                nameof(advancePercentage));
        }

        if (decimal.Round(advancePercentage, 4) != advancePercentage)
        {
            throw new ArgumentException(
                $"'{advancePercentage.ToString(CultureInfo.InvariantCulture)}%' is finer than {MaxPercentageScale.ToString(CultureInfo.InvariantCulture)}%, "
                + "which no currency can settle. Keep the percentage at four decimal places or fewer.",
                nameof(advancePercentage));
        }

        if (balanceTerm.Kind is PaymentTermKind.Advance or PaymentTermKind.PartialAdvance)
        {
            throw new ArgumentException(
                "The balance of a partial advance is scheduled by days or a fixed day, not by another advance.",
                nameof(balanceTerm));
        }

        return new PaymentTerm(PaymentTermKind.PartialAdvance, null, null, advancePercentage, balanceTerm);
    }

    /// <summary>
    /// When the term's balance falls due against a document dated <paramref name="documentDate"/>. For a full
    /// advance this is the document date itself: nothing may ship before it.
    /// </summary>
    /// <remarks>
    /// A fixed day that does not exist in the following month (the 31st in Falgun) resolves to that month's
    /// last day. That is a calendar fact rather than a clamp: the alternative is refusing to save a term the
    /// tenant legitimately agreed, and a payment cannot be made on a date that has no day.
    /// </remarks>
    public DateOnly DueOn(DateOnly documentDate) => Kind switch
    {
        PaymentTermKind.NetDays => documentDate.AddDays(NetDays!.Value),
        PaymentTermKind.FixedDayOfMonth => FixedDayDue(documentDate, DayOfMonth!.Value),
        PaymentTermKind.Advance => documentDate,
        PaymentTermKind.PartialAdvance => Balance!.DueOn(documentDate),
        _ => throw new ArgumentOutOfRangeException(nameof(documentDate), Kind, "Unknown payment term kind."),
    };

    /// <summary>The amount payable now, before any goods move.</summary>
    public Money AdvanceDue(Money orderValue) => Kind switch
    {
        PaymentTermKind.Advance => orderValue,
        PaymentTermKind.PartialAdvance => Money.Round(
            orderValue.Currency,
            orderValue.Amount * AdvancePercentage!.Value / 100m),
        _ => orderValue.Currency.Zero(),
    };

    /// <summary>
    /// What remains after the advance. Derived by subtraction from the amount just computed, so the two
    /// halves always re-add to the order value exactly and the rounding residue is on one side only.
    /// </summary>
    public Money BalanceDue(Money orderValue) => Money.Subtract(orderValue, AdvanceDue(orderValue));

    private static DateOnly FixedDayDue(DateOnly documentDate, int dayOfMonth)
    {
        var month = documentDate.AddMonths(1);
        var lastDay = DateTime.DaysInMonth(month.Year, month.Month);
        var day = dayOfMonth > lastDay ? lastDay : dayOfMonth;

        return new DateOnly(month.Year, month.Month, day);
    }
}
