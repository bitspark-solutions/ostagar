namespace Ostagar.Modules.CashBook;

public static class BankAccountRules
{
    public static void Validate(BankAccount account, bool postableBankAccount)
    {
        if (!postableBankAccount || string.IsNullOrWhiteSpace(account.Code)
            || account.Code.Length > BankingVocabulary.CodeLength
            || string.IsNullOrWhiteSpace(account.BankName)
            || account.BankName.Length > BankingVocabulary.BankNameLength
            || string.IsNullOrWhiteSpace(account.AccountNumber)
            || account.AccountNumber.Length > BankingVocabulary.AccountNumberLength)
            throw new BankingRuleException(BankingErrorCode.InvalidAccount);
    }
}
