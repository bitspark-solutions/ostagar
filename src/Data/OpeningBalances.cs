using Ostagar.Core;
using Ostagar.Ledger;
using Ostagar.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ostagar.Data;

/// <summary>
/// Turns a hand-written opening balance sheet into one posted journal entry.
/// </summary>
/// <remarks>
/// <para>
/// <b>The difference is the owner's money, and it is named.</b> A balance sheet typed from paper rarely
/// ties: the accountant's list of accounts sums to 4,000,000 on the asset side and 3,975,000 on the
/// other. Every real ERP posts that 25,000 to capital and shows it, because the alternative — a
/// validator that refuses the import until the owner finds the 25,000 — sends someone back through a
/// shoebox of receipts to reconcile a figure that is almost certainly their own equity. Making the
/// assumption explicit and inspectable is the honest version, and it is why the code posts one entry
/// rather than editing balances until they agree.
/// </para>
///
/// <para>
/// <b>Sides come from account type, not from the caller.</b> Which side an account belongs on is
/// already decided in exactly one place, <see cref="NormalBalanceOf"/>; asking an importer to state it
/// would reintroduce the legacy build's three disagreeing definitions of the same sign.
/// </para>
///
/// <para>
/// Everything after this — numbering, balance checking, the period, immutability — is <see cref="IPoster"/>'s
/// job. Opening balances are an ordinary posting once the sides exist, and special-casing them in the
/// write path would be the one place a rule could be weaker for the founding entry than for a Friday
/// correction.
/// </para>
/// </remarks>
public sealed class OpeningBalances(
    OstagarDbContext context,
    ITenantContext tenantContext,
    IPoster poster) : IOpeningBalances
{
    public async Task<PostingOutcome> PostAsync(
        int fiscalYear,
        IReadOnlyList<OpeningBalance> balances,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId;

        if (tenantId.IsEmpty)
        {
            throw new InvalidOperationException(
                "Opening balances need a resolved tenant. Without one, the entry would be written "
                + "against a tenant id nobody chose.");
        }

        if (balances.Count == 0)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.NoBalances,
                "No opening balances were given. A company can legitimately start from nothing, but "
                + "that has to be said rather than inferred from an empty import.")]);
        }

        var tenant = await context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(each => each.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            throw new InvalidOperationException(
                $"Tenant {tenantId} has no directory row, so its functional currency is unknown.");
        }

        var currency = Currency.Of(CurrencyCode.From(tenant.FunctionalCurrencyCode));

        var accounts = await context.Accounts
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var refused = RefuseUnknownCodes(balances, accounts);
        if (refused is not null)
        {
            return refused;
        }

        var byCode = accounts.ToDictionary(account => account.Code, StringComparer.Ordinal);

        var lines = new List<PostingLine>();
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        foreach (var balance in balances)
        {
            var account = byCode[balance.AccountCode];

            if (!account.IsPostable || !account.IsActive)
            {
                return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                    PostingErrorCode.AccountUnpostable,
                    $"'{account.Code} {account.Name}' is a grouping account. Put the balance on the "
                    + "accounts underneath it, not on the total.")]);
            }

            var amount = Money.Round(currency, Math.Abs(balance.Amount));

            // Zero balances are skipped rather than posted as empty lines. A company's chart has more
            // accounts than it uses, and a trial balance padded with 0.00 rows makes the ones that carry
            // anything harder to see.
            if (amount.IsZero)
            {
                continue;
            }

            // Named for what it decides rather than for the balance's own sign: "on normal side" reads
            // as true for a liability holding a normal credit balance, and the line it produces is a
            // credit, so the obvious name is the one that gets a reader to write the inversion.
            var postAsDebit = balance.Amount >= 0m
                ? NormalBalanceOf.For(account.Type) == NormalBalance.Debit
                : NormalBalanceOf.For(account.Type) == NormalBalance.Credit;

            lines.Add(postAsDebit
                ? DebitLine(account, amount, currency)
                : CreditLine(account, amount, currency));

            if (postAsDebit)
            {
                totalDebit += amount.Amount;
            }
            else
            {
                totalCredit += amount.Amount;
            }
        }

        if (lines.Count == 0)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.NoLines,
                "Every balance given was zero, so there is nothing to open. Post the company's first "
                + "document instead of an entry that says nothing.")]);
        }

        var residual = totalDebit - totalCredit;

        if (residual != 0m)
        {
            var capital = await OwnersCapitalAccountAsync(byCode).ConfigureAwait(false);
            if (capital.Rejected is not null)
            {
                return capital.Rejected;
            }

            var amount = Money.Round(currency, Math.Abs(residual));

            // Debits exceeding credits means the company holds more than its liabilities and recorded
            // equity account for — which is the owner's money, and it belongs to capital as a credit.
            lines.Add(residual > 0m
                ? CreditLine(capital.Account!, amount, currency)
                : DebitLine(capital.Account!, amount, currency));
        }

        var period = await context.FiscalPeriods
            .Where(each => each.FiscalYear == fiscalYear && each.Number == 1)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (period is null)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.PeriodNotFound,
                $"Fiscal year {fiscalYear} has no first period. Provision the company's books before "
                + "opening its balances.")]);
        }

        return await poster.PostAsync(new PostingRequest
        {
            JournalCode = StandardJournals.OpeningCode,
            PostingDate = period.StartDate,
            // Keyed on the tenant, so a second run of the same import resolves to this entry rather
            // than a second one, and the only way to change the founding figures is to reverse it.
            SourceType = "ledger.opening",
            SourceId = tenantId.Value,
            Memo = $"Opening balances for fiscal year {fiscalYear}",
            ActorUserId = actorUserId,
            Lines = lines,
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reports every unknown code at once, and nothing else.
    /// </summary>
    /// <remarks>
    /// All of them in one message rather than the first: an opening balance sheet is transcribed from a
    /// document, and telling someone one misread code per attempt turns twelve corrections into twelve
    /// round trips.
    ///
    /// Duplicates are refused outright instead of being summed. Whether "Cash on Hand 1000" listed twice
    /// means 2,000 or means the sheet was counted twice has only one safe answer when the result is a
    /// company's opening balance sheet.
    /// </remarks>
    private static PostingOutcome? RefuseUnknownCodes(
        IReadOnlyList<OpeningBalance> balances,
        IReadOnlyList<ChartOfAccount> accounts)
    {
        var known = accounts.Select(account => account.Code).ToHashSet(StringComparer.Ordinal);

        var unknown = balances
            .Select(balance => balance.AccountCode)
            .Where(code => !known.Contains(code))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (unknown.Count > 0)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.UnknownAccountCodes,
                $"No account exists with code {(unknown.Count == 1 ? "" : "any of ")}"
                + $"{string.Join(", ", unknown)}.")]);
        }

        var duplicated = balances
            .GroupBy(balance => balance.AccountCode, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (duplicated.Count > 0)
        {
            return new PostingOutcome(PostingOutcomeStatus.Rejected, [new PostingError(
                PostingErrorCode.DuplicateAccountCode,
                $"Account {(duplicated.Count == 1 ? "code" : "codes")} {string.Join(", ", duplicated)} "
                + $"{(duplicated.Count == 1 ? "appears" : "appear")} more than once. Combine them into "
                + "one balance before importing.")]);
        }

        return null;
    }

    private static Task<(ChartOfAccount? Account, PostingOutcome? Rejected)> OwnersCapitalAccountAsync(
        Dictionary<string, ChartOfAccount> byCode)
    {
        if (byCode.TryGetValue(BaseChartOfAccounts.OwnersCapitalCode, out var capital))
        {
            return Task.FromResult<(ChartOfAccount?, PostingOutcome?)>((capital, null));
        }

        // The difference has to land somewhere the owner can find. Defaulting it to whatever equity
        // account happens to sort first would put the founding assumption of a business in a place no
        // one chose, discoverable only by someone reading the SQL.
        return Task.FromResult<(ChartOfAccount?, PostingOutcome?)>((null, new PostingOutcome(
            PostingOutcomeStatus.Rejected,
            [new PostingError(
                PostingErrorCode.AccountUnpostable,
                $"This company's chart has no account {BaseChartOfAccounts.OwnersCapitalCode} to "
                + "receive the difference between its sides. Add one, or make the balances tie exactly.")])));
    }

    private static PostingLine DebitLine(ChartOfAccount account, Money amount, Currency currency) => new()
    {
        AccountId = account.Id,
        Debit = amount,
        AmountInCurrency = amount.Amount,
        Currency = currency.Code,
    };

    private static PostingLine CreditLine(ChartOfAccount account, Money amount, Currency currency) => new()
    {
        AccountId = account.Id,
        Credit = amount,
        AmountInCurrency = amount.Amount,
        Currency = currency.Code,
    };
}
