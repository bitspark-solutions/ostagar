namespace Ostagar.Modules.CashBook;

public enum BankingErrorCode
{
    InvalidAccount,
    AccountNotFound,
    SameAccount,
    CurrencyMismatch,
    AmountNotPositive,
    InvalidIdentity,
    InvalidMemo,
    RateNotFound,
    InvalidDateRange,
    StatementUnbalanced,
    InvalidStatementLines,
    AlreadyReconciled,
    StatementSequenceInvalid,
    RetryConflict,
    DatabaseRefused,
}
