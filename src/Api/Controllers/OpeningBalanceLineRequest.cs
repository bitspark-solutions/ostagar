namespace Ostagar.Api.Controllers;

/// <summary>
/// One account's opening balance on the day a company starts being recorded.
/// </summary>
/// <param name="AccountCode">The account's human-facing code — the identifier a balance sheet carries,
/// not a GUID the importer cannot know. Unknown codes are refused with the whole list at once.</param>
/// <param name="Amount">Signed on the account's normal side: positive means the balance stands where
/// the account type expects it, negative means the opposite side (an overdrawn bank, a customer who
/// overpaid). Which side that is comes from the account, never from the payload.</param>
public sealed record OpeningBalanceLineRequest(string AccountCode, decimal Amount);
