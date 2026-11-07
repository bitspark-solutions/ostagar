namespace Ostagar.Api.Controllers;

/// <summary>
/// One posted line, as the ledger holds it.
/// </summary>
/// <param name="Id">Row identifier.</param>
/// <param name="AccountId">The account posted to.</param>
/// <param name="AccountCode">Resolved at read time so a screen does not need a second request to show a
/// code a reader can actually recognise.</param>
/// <param name="LineOrder">Order within the entry.</param>
/// <param name="Debit">Debit amount, null when the line is a credit.</param>
/// <param name="Credit">Credit amount, null when the line is a debit.</param>
/// <param name="Currency">The line's own currency, which may differ from the company's.</param>
/// <param name="AmountInCurrency">The amount as the source document stated it.</param>
/// <param name="ExchangeRate">The rate the ledger resolved or accepted for the line.</param>
/// <param name="Memo">Line note.</param>
public sealed record JournalLineResponse(
    Guid Id,
    Guid AccountId,
    string AccountCode,
    int LineOrder,
    decimal? Debit,
    decimal? Credit,
    string Currency,
    decimal AmountInCurrency,
    decimal ExchangeRate,
    string Memo);
