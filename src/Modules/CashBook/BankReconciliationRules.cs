namespace Ostagar.Modules.CashBook;

public static class BankReconciliationRules
{
    public static void Validate(DateOnly from, DateOnly to, decimal opening, decimal closing,
        IReadOnlyList<decimal> movements, int requestedCount, int distinctCount)
    {
        if (from == default || to < from)
            throw new BankingRuleException(BankingErrorCode.InvalidDateRange);
        if (requestedCount != distinctCount || requestedCount != movements.Count)
            throw new BankingRuleException(BankingErrorCode.InvalidStatementLines);
        if (opening + movements.Sum() != closing)
            throw new BankingRuleException(BankingErrorCode.StatementUnbalanced);
    }

    public static void ValidateSequence(BankReconciliation? previous, DateOnly from, decimal opening)
    {
        if (previous is not null && (from.DayNumber - previous.To.DayNumber != BankingVocabulary.StatementDayStep
            || opening != previous.ClosingBalance))
            throw new BankingRuleException(BankingErrorCode.StatementSequenceInvalid);
    }
}
