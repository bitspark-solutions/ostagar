namespace Ostagar.Modules.CashBook;

public sealed class BankingRuleException(BankingErrorCode code) : Exception(code.ToString())
{
    public BankingErrorCode Code { get; } = code;
}
