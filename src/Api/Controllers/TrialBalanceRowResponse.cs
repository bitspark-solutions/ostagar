namespace Ostagar.Api.Controllers;

/// <summary>
/// One account's column of the trial balance.
/// </summary>
/// <param name="AccountId">The account the row aggregates.</param>
/// <param name="Code">Human-facing account code — the identifier a reader cites, not the id.</param>
/// <param name="Name">Account name.</param>
/// <param name="NameBangla">Bangla account name for printed reports.</param>
/// <param name="Type">Account type, which decides which side the balance reads as normal.</param>
/// <param name="TotalDebit">Sum of the account's debit-side movements in scope.</param>
/// <param name="TotalCredit">Sum of the account's credit-side movements in scope.</param>
/// <param name="Balance">Signed balance on the account's normal side (debit - credit for assets and
/// expenses, credit - debit for liabilities, equity and income). Negative means the account stands on
/// its opposite side — a fact, not an error to hide by taking absolute value.</param>
public sealed record TrialBalanceRowResponse(
    Guid AccountId,
    string Code,
    string Name,
    string NameBangla,
    string Type,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal Balance);
