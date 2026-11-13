namespace Ostagar.Modules.CashBook;

public static class BankingVocabulary
{
    public const string TransferSource = "bank.transfer";
    public const string TransferRetryAction = "bank.transfer.retry";
    public const string AccountAction = "bank.account.create";
    public const string ReconcileAction = "bank.reconcile";
    public const string Resource = "bank";
    public const int CodeLength = 32;
    public const int BankNameLength = 200;
    public const int AccountNumberLength = 80;
    public const int MemoLength = 500;
    public const int CurrencyLength = 3;
    public const int StatementDayStep = 1;
    public const string AmountType = "numeric(18,4)";
    public const string WriteRoles = "Owner,Accounts";
}
