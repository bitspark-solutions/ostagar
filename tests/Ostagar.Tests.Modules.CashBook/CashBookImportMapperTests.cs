using Ostagar.Core;
using Ostagar.Modules.CashBook;

namespace Ostagar.Tests.Modules.CashBook;

public sealed class CashBookImportMapperTests
{
    private static readonly Guid CashAccount = Guid.NewGuid();
    private static readonly Guid ExpenseAccount = Guid.NewGuid();

    [Fact]
    public void A_mapped_category_becomes_the_counter_account()
    {
        var draft = CashBookImportMapper.Map(Side(), CashAccount, Currency.Of(CurrencyCode.Bdt), Map());

        Assert.True(draft.Succeeded);
        var transaction = draft.Transaction!;
        Assert.Equal(CashTransactionKind.Payment, transaction.Kind);
        Assert.Equal(CashAccount, transaction.CashAccountId);
        Assert.Equal(ExpenseAccount, transaction.CounterAccountId);
        Assert.Equal(2_400m, transaction.Amount!.Value.Amount);
        Assert.Equal(CurrencyCode.Bdt, transaction.Amount.Value.Currency.Code);
    }

    [Fact]
    public void An_unmapped_category_is_refused_naming_the_category_not_auto_created()
    {
        var draft = CashBookImportMapper.Map(
            Side(), CashAccount, Currency.Of(CurrencyCode.Bdt),
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));

        Assert.False(draft.Succeeded);
        Assert.Equal(CashBookImportErrorCode.CategoryUnmapped, draft.Error!.Code);
        Assert.Contains("Subcontract bill", draft.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_matching_is_case_insensitive_because_the_book_is_hand_typed()
    {
        var draft = CashBookImportMapper.Map(
            Side() with { Category = "subcontract BILL" },
            CashAccount,
            Currency.Of(CurrencyCode.Bdt),
            Map());

        Assert.True(draft.Succeeded);
    }

    [Fact]
    public void Supplier_and_buyer_survive_into_the_memo()
    {
        var draft = CashBookImportMapper.Map(
            Side() with { Particulars = "joggers", Supplier = "KDS", Buyer = "Brooklyn" },
            CashAccount,
            Currency.Of(CurrencyCode.Bdt),
            Map());

        Assert.Equal("joggers | supplier: KDS | buyer: Brooklyn", draft.Transaction!.Memo);
    }

    private static CashBookImportSide Side() => new()
    {
        Kind = CashTransactionKind.Payment,
        LineNumber = 4,
        TransactionDate = new DateOnly(2025, 2, 1),
        Category = "Subcontract bill",
        Amount = 2_400m,
    };

    private static Dictionary<string, Guid> Map() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Subcontract bill"] = ExpenseAccount,
        };
}
