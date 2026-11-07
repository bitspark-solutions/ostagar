using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// A rate the ledger resolved for a date, together with the row that supplied it.
/// </summary>
/// <remarks>
/// <see cref="ValidFrom"/> is kept because "112.40" is a different claim on its own day from
/// "112.40, which has been in force since the 3rd". A backdated posting that used a rate two weeks older
/// than its own date has to be explainable, and the only way to explain it is to name which row was
/// chosen and when that row began to apply.
/// </remarks>
/// <param name="Currency">The currency being priced.</param>
/// <param name="ValidFrom">First day this row applies.</param>
/// <param name="Rate">Units of functional currency per one unit of <paramref name="Currency"/>.</param>
public sealed record ExchangeRateQuote(CurrencyCode Currency, DateOnly ValidFrom, decimal Rate);
