using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// Reads and records the company's effective-dated rate history.
/// </summary>
/// <remarks>
/// The read is deliberately a <b>find</b> that can come back empty rather than a resolve that throws. A
/// company that has never recorded a rate for a currency is in a different situation from one whose rate
/// runs out at a date, and collapsing both into an exception loses the distinction the caller needs: the
/// first should be offered the chance to record one, the second has to be told which date it has no
/// price for.
/// </remarks>
public interface IExchangeRates
{
    /// <summary>
    /// The newest row for <paramref name="currency"/> that applies on <paramref name="postingDate"/>, or null.
    /// </summary>
    Task<ExchangeRateQuote?> FindAsync(
        CurrencyCode currency,
        DateOnly postingDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends one effective-dated rate. Never changes an existing one.
    /// </summary>
    Task<PostingOutcome> RecordAsync(
        ExchangeRateQuote quote,
        string source,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
