using Microsoft.EntityFrameworkCore;
using Ostagar.Core;
using Ostagar.Ledger;
using Ostagar.Modules.CashBook;
using Ostagar.Platform.Audit;

namespace Ostagar.Data;

public sealed class Banking(OstagarDbContext context, IPoster poster, IExchangeRates rates, IAuditWriter audit)
{
    public async Task<BankAccount> CreateAccountAsync(Guid ledgerAccountId, string code, string bankName,
        string accountNumber, CurrencyCode currency, Guid actor, CancellationToken ct)
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), LedgerAccountId = ledgerAccountId, Code = code.Trim(),
            BankName = bankName.Trim(), AccountNumber = accountNumber.Trim(), Currency = currency,
        };
        var postable = await context.Accounts.AnyAsync(x => x.Id == ledgerAccountId
            && x.SubType == AccountSubType.Bank && x.IsActive && x.IsPostable, ct);
        BankAccountRules.Validate(account, postable);
        context.BankAccounts.Add(account);
        Append(actor, BankingVocabulary.AccountAction, account.Id, new { account.Code, account.LedgerAccountId, Currency = currency.Value });
        await context.SaveChangesAsync(ct);
        return account;
    }

    public async Task<PostingOutcome> TransferAsync(BankTransferDraft draft, Guid actor, CancellationToken ct)
    {
        var from = await AccountAsync(draft.FromBankAccountId, ct);
        var to = await AccountAsync(draft.ToBankAccountId, ct);
        var functionalCode = await context.Tenants.Where(x => x.Id == context.CurrentTenantId)
            .Select(x => x.FunctionalCurrencyCode).SingleAsync(ct);
        var functional = Currency.Of(CurrencyCode.From(functionalCode));
        var amount = new Money(Currency.Of(from.Currency), draft.Amount);
        var quote = from.Currency == functional.Code ? null : await rates.FindAsync(from.Currency, draft.PostingDate, ct);
        if (from.Currency != functional.Code && quote is null)
            throw new BankingRuleException(BankingErrorCode.RateNotFound);
        var converted = Money.Round(functional, amount.Amount * (quote?.Rate ?? decimal.One));
        var request = BankTransferPosting.Build(draft.Id, from, to, draft.PostingDate, amount, converted, actor, draft.Memo);
        var existing = await context.GlEntries.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.SourceType == BankingVocabulary.TransferSource && x.SourceId == draft.Id, ct);
        if (existing is not null)
        {
            EnsureMatchesDraft(existing, draft, from, to);
            Append(actor, BankingVocabulary.TransferRetryAction, existing.Id, new { draft.Id, existing.Number });
            await context.SaveChangesAsync(ct);
            return new PostingOutcome(PostingOutcomeStatus.AlreadyPosted, [], existing.Id, existing.Number);
        }
        var outcome = await poster.PostAsync(request, ct);
        if (outcome.Status == PostingOutcomeStatus.AlreadyPosted)
        {
            var committed = await context.GlEntries.AsNoTracking().Include(x => x.Lines)
                .SingleAsync(x => x.SourceType == BankingVocabulary.TransferSource && x.SourceId == draft.Id, ct);
            EnsureMatchesDraft(committed, draft, from, to);
        }
        return outcome;
    }

    private static void EnsureMatchesDraft(GlEntry existing, BankTransferDraft draft, BankAccount from, BankAccount to)
    {
        var debit = existing.Lines.Single(x => x.Debit != null);
        var credit = existing.Lines.Single(x => x.Credit != null);
        if (existing.PostingDate != draft.PostingDate || existing.Memo != draft.Memo
            || debit.AccountId != to.LedgerAccountId || credit.AccountId != from.LedgerAccountId
            || debit.AmountInCurrency != draft.Amount || debit.Currency != from.Currency)
            throw new BankingRuleException(BankingErrorCode.RetryConflict);
    }

    public async Task<BankReconciliation> ReconcileAsync(Guid bankId, BankReconciliationDraft draft, Guid actor, CancellationToken ct)
    {
        if (draft.Id == Guid.Empty || actor == Guid.Empty)
            throw new BankingRuleException(BankingErrorCode.InvalidIdentity);
        var bank = await AccountAsync(bankId, ct);
        _ = new Money(Currency.Of(bank.Currency), draft.OpeningBalance);
        _ = new Money(Currency.Of(bank.Currency), draft.ClosingBalance);
        var existing = await context.BankReconciliations.Include(x => x.Matches).SingleOrDefaultAsync(x => x.Id == draft.Id, ct);
        if (existing is not null)
        {
            if (existing.BankAccountId != bankId || existing.From != draft.From || existing.To != draft.To
                || existing.OpeningBalance != draft.OpeningBalance || existing.ClosingBalance != draft.ClosingBalance
                || !existing.Matches.Select(x => x.GlLineId).Order().SequenceEqual(draft.GlLineIds.Order()))
                throw new BankingRuleException(BankingErrorCode.RetryConflict);
            return existing;
        }
        var previous = await context.BankReconciliations.Where(x => x.BankAccountId == bankId)
            .OrderByDescending(x => x.To).FirstOrDefaultAsync(ct);
        BankReconciliationRules.ValidateSequence(previous, draft.From, draft.OpeningBalance);
        var movements = await context.GlLines.Where(x => draft.GlLineIds.Contains(x.Id)
                && x.AccountId == bank.LedgerAccountId && x.Currency == bank.Currency)
            .Join(context.GlEntries.Where(x => x.Status == GlEntryStatus.Posted && x.PostingDate <= draft.To),
                x => x.EntryId, x => x.Id, (line, entry) => line.Debit != null ? line.AmountInCurrency : -line.AmountInCurrency)
            .ToListAsync(ct);
        BankReconciliationRules.Validate(draft.From, draft.To, draft.OpeningBalance, draft.ClosingBalance,
            movements, draft.GlLineIds.Count, draft.GlLineIds.Distinct().Count());
        if (await context.BankReconciliationMatches.AnyAsync(x => draft.GlLineIds.Contains(x.GlLineId), ct))
            throw new BankingRuleException(BankingErrorCode.AlreadyReconciled);
        var statement = new BankReconciliation
        {
            Id = draft.Id, BankAccountId = bankId, From = draft.From, To = draft.To,
            OpeningBalance = draft.OpeningBalance, ClosingBalance = draft.ClosingBalance,
            ActorUserId = actor, CreatedAt = DateTimeOffset.UtcNow,
            Matches = draft.GlLineIds.Select(id => new BankReconciliationMatch
                { Id = Guid.NewGuid(), ReconciliationId = draft.Id, GlLineId = id }).ToList(),
        };
        context.BankReconciliations.Add(statement);
        Append(actor, BankingVocabulary.ReconcileAction, statement.Id,
            new { bankId, draft.From, draft.To, draft.OpeningBalance, draft.ClosingBalance, draft.GlLineIds });
        await context.SaveChangesAsync(ct);
        return statement;
    }

    public async Task<BankStatementReport?> StatementAsync(Guid id, DateOnly asOf, CancellationToken ct)
    {
        if (asOf == default) throw new BankingRuleException(BankingErrorCode.InvalidDateRange);
        var bank = await context.BankAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (bank is null) return null;
        await using var snapshot = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var rows = await context.GlLines.AsNoTracking().Where(x => x.AccountId == bank.LedgerAccountId && x.Currency == bank.Currency)
            .Join(context.GlEntries.Where(x => x.Status == GlEntryStatus.Posted && x.PostingDate <= asOf),
                x => x.EntryId, x => x.Id, (line, entry) => new BankStatementLine(
                    line.Id, line.EntryId, entry.Number, entry.PostingDate, line.Memo,
                    line.Debit != null ? line.AmountInCurrency : -line.AmountInCurrency,
                    context.BankReconciliationMatches.Any(match => match.GlLineId == line.Id
                        && context.BankReconciliations.Any(s => s.Id == match.ReconciliationId && s.To <= asOf))))
            .ToListAsync(ct);
        var closing = await context.BankReconciliations.Where(x => x.BankAccountId == id && x.To <= asOf)
            .OrderByDescending(x => x.To).Select(x => (decimal?)x.ClosingBalance).FirstOrDefaultAsync(ct);
        await snapshot.CommitAsync(ct);
        return new BankStatementReport(bank.Currency.Value, asOf, closing,
            rows.OrderBy(x => x.PostingDate).ThenBy(x => x.Number, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray());
    }

    private async Task<BankAccount> AccountAsync(Guid id, CancellationToken ct) =>
        await context.BankAccounts.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new BankingRuleException(BankingErrorCode.AccountNotFound);

    private void Append(Guid actor, string action, Guid id, object detail) => audit.Append(new AuditRecord(
        actor, string.Empty, action, BankingVocabulary.Resource, id.ToString(), AuditOutcome.Allowed, detail));
}
