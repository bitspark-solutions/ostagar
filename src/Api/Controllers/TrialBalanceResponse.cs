namespace Ostagar.Api.Controllers;

/// <summary>
/// The trial balance: every account's debit and credit column, and the proof that the two sides tie.
/// </summary>
/// <param name="Rows">One row per account with activity in scope, ordered by account code.</param>
/// <param name="TotalDebit">Sum of all debit columns. For a correct ledger this equals
/// <paramref name="TotalCredit"/> — and this report is how a reader checks that without trusting it.</param>
/// <param name="TotalCredit">Sum of all credit columns.</param>
/// <param name="IsBalanced">Whether the two column totals tie exactly. Decimal equality, no tolerance:
/// an unbalanced trial balance is the single most important symptom this report exists to surface.</param>
public sealed record TrialBalanceResponse(
    IReadOnlyList<TrialBalanceRowResponse> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced);
