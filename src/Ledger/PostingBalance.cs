using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// Checks a proposed posting against the rules that make it a valid double-entry.
/// </summary>
/// <remarks>
/// <para>This is the application-side mirror of database constraints, not a replacement for them. The
/// triggers and CHECKs added with the ledger migration enforce the same rules; a caller that skips this
/// class gets a constraint violation instead of a readable error, and that is the point — the invariant
/// does not depend on anyone remembering to validate.</para>
///
/// <para><b>No tolerance.</b> Amounts are <c>decimal</c> at a known scale, so equality is exact and the
/// legacy question "how far off is close enough" has no answer here. Its ±0.01 was one whole minor unit,
/// wide enough to conceal a missing rounding policy entirely.</para>
///
/// <para><b>Two lines minimum.</b> A single-line posting is what made the legacy trial balance unable to
/// balance: the cash-book path wrote one-sided entries straight to the ledger with no check at all.</para>
/// </remarks>
public static class PostingBalance
{
    public static PostingBalanceResult Check(IReadOnlyList<PostingLine> lines)
    {
        var errors = new List<PostingError>();

        if (lines.Count == 0)
        {
            return new PostingBalanceResult(false, [new PostingError(PostingErrorCode.NoLines, "No lines were proposed.")], 0m, 0m);
        }

        if (lines.Count == 1)
        {
            errors.Add(new PostingError(
                PostingErrorCode.SingleSided,
                "A posting needs at least two lines; a single line cannot be double-entry.",
                0));
        }

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;
        CurrencyCode? functional = null;
        var currencyTotals = new Dictionary<CurrencyCode, (decimal Debit, decimal Credit)>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];

            if ((line.Debit is null) == (line.Credit is null))
            {
                errors.Add(new PostingError(
                    PostingErrorCode.LineSides,
                    "Exactly one of debit or credit must be set on a line.",
                    index));
            }

            var side = line.Debit ?? line.Credit;

            if (side is not null && side.Value.IsZero)
            {
                errors.Add(new PostingError(PostingErrorCode.ZeroAmount, "A line amount must not be zero.", index));
            }

            // Set before the rate test below: which currency is functional is decided by the first line
            // that carries an amount, and a check that runs before it would read every first line as
            // foreign and demand a rate for a taka posting.
            if (side is not null)
            {
                functional ??= side.Value.Currency.Code;
            }

            // Two separate rules, and keeping them apart matters. A rate that is present must be positive
            // whatever the line's currency, because a zero or negative number beside a taka line is a
            // defect just as much as beside a dollar one. A rate is only <i>required</i> when the line is
            // foreign — demanding one on a domestic line would mean every local posting carries a number
            // that is 1 by definition and can only ever be wrong or noise.
            if (line.ExchangeRate is not null && line.ExchangeRate <= 0m)
            {
                errors.Add(new PostingError(
                    PostingErrorCode.InvalidExchangeRate,
                    "An exchange rate must be positive so it can be inverted and explained.",
                    index));
            }
            else if (functional is not null && line.Currency != functional && line.ExchangeRate is null)
            {
                errors.Add(new PostingError(
                    PostingErrorCode.MissingExchangeRate,
                    $"Line {index + 1} is in {line.Currency} and no rate was resolved for it.",
                    index));
            }

            if (side is not null)
            {
                if (side.Value.Currency.Code != functional)
                {
                    errors.Add(new PostingError(
                        PostingErrorCode.MixedFunctionalCurrency,
                        $"Line {index + 1} is in {side.Value.Currency.Code} but the posting is in {functional}.",
                        index));
                }

                if (line.Debit is not null)
                {
                    totalDebit += side.Value.Amount;
                }
                else
                {
                    totalCredit += side.Value.Amount;
                }
            }

            // When a line is already in functional currency, its Money is the source of truth and
            // AmountInCurrency carries no extra information. Deriving the amount instead of trusting the
            // field means a caller that builds a taka line and never sets AmountInCurrency cannot silently
            // contribute zero to the per-currency totals — which would report a balancing failure in a
            // currency the posting does not even trade in, hiding the actual mistake.
            var amountInCurrency = side is not null && line.Currency == side.Value.Currency.Code
                ? side.Value.Amount
                : line.AmountInCurrency;

            var current = currencyTotals.GetValueOrDefault(line.Currency, (0m, 0m));

            currencyTotals[line.Currency] = line.Debit is not null
                ? (current.Item1 + amountInCurrency, current.Item2)
                : (current.Item1, current.Item2 + amountInCurrency);
        }

        if (totalDebit != totalCredit)
        {
            errors.Add(new PostingError(
                PostingErrorCode.Unbalanced,
                $"Debits total {totalDebit.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
                + $"and credits total {totalCredit.ToString(System.Globalization.CultureInfo.InvariantCulture)}; "
                + "they must be equal."));
        }

        foreach (var (currency, totals) in currencyTotals)
        {
            if (totals.Item1 != totals.Item2)
            {
                errors.Add(new PostingError(
                    PostingErrorCode.UnbalancedInCurrency,
                    $"{currency} debits total {totals.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture)} "
                    + $"and credits total {totals.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture)}. "
                    + "Balancing only in functional currency hides the difference as an unrecorded FX gain or loss."));
            }
        }

        return new PostingBalanceResult(errors.Count == 0, errors, totalDebit, totalCredit)
        {
            CurrencyTotals = currencyTotals,
            RoundingResidual = totalDebit - totalCredit,
        };
    }

    /// <summary>
    /// Builds the line that absorbs a rounding difference, or null when there is none.
    /// </summary>
    /// <remarks>
    /// Rounding a document total and rounding its lines give different answers — the classic case is a
    /// 52 Kr total that becomes 7.92 or 7.93 per line. Oracle's answer is to post the difference to a
    /// designated header rounding account, which is what this supports: the residual is recorded, not
    /// silently dropped from one line.
    /// </remarks>
    public static PostingLine? ResidualLine(Guid roundingAccountId, decimal residual, Currency currency, string memo)
    {
        if (residual == 0m)
        {
            return null;
        }

        var amount = Money.Round(currency, System.Math.Abs(residual));

        return residual > 0m
            ? new PostingLine
            {
                AccountId = roundingAccountId,
                Credit = amount,
                AmountInCurrency = amount.Amount,
                Currency = currency.Code,
                Memo = memo,
            }
            : new PostingLine
            {
                AccountId = roundingAccountId,
                Debit = amount,
                AmountInCurrency = amount.Amount,
                Currency = currency.Code,
                Memo = memo,
            };
    }
}
