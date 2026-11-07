namespace Ostagar.Tests.Integration;

/// <summary>
/// What a posted line actually stored for its rate and its two amounts.
/// </summary>
/// <remarks>
/// Read back from the row rather than from the request that produced it, because the claim under test is
/// which rate the ledger <i>chose</i>. A resolution that picked the wrong month would still have posted
/// the amount the caller supplied and looked correct from the calling side.
/// </remarks>
internal sealed record RateOnLine(decimal ExchangeRate, decimal AmountInCurrency, decimal FunctionalAmount);
