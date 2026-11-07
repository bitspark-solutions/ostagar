namespace Ostagar.Ledger;

/// <summary>
/// One account's balance at the moment a company starts being recorded.
/// </summary>
/// <remarks>
/// <para>
/// <b>Signed on the account's own normal side</b>, which is the convention
/// <see cref="NormalBalanceOf.Balance"/> already uses everywhere else in the product, so one sign means
/// one thing from import through to report. A loan of 150,000 is therefore <c>+150_000</c> and not
/// <c>-150_000</c>: a credit-normal account holding its normal balance is positive. Stated with the
/// example because the other reading — assets positive, liabilities negative, the way a hand-typed
/// trial balance is often columned — is at least as natural, and picking it wrongly does not fail. It
/// inverts every opening balance and still produces a perfectly balanced entry.
/// </para>
///
/// <para>
/// A negative <paramref name="Amount"/> is allowed and means the account sits on the opposite side to its
/// normal one: a bank overdraft and a customer who has overpaid are both real, and forcing them onto
/// their normal side would misstate the balance and hide the fact.
/// </para>
///
/// <para>
/// Functional currency only, and not an oversight. Opening a foreign-currency balance needs the rate in
/// force on the opening date, which means an effective-dated rate store — the remaining item in this
/// phase. Accepting a currency before that exists would mean either inventing a rate or storing one the
/// ledger cannot later re-derive, and both look harmless until a year-end revaluation contradicts them.
/// </para>
/// </remarks>
/// <param name="AccountCode">The company's own account code.</param>
/// <param name="Amount">Balance in the company's functional currency, positive on the account's normal side.</param>
public sealed record OpeningBalance(string AccountCode, decimal Amount);
