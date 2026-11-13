using Ostagar.Modules.CashBook;

namespace Ostagar.Tests.Modules.CashBook;

/// <summary>
/// The client layout these fixtures mirror: a header whose category column is misspelt
/// "Catagories" in the real files, then ten-column rows pairing a money-in half
/// (date dd-MM-yyyy, category, particulars, amount) with a money-out half
/// (date dd-MM-yy, category, supplier, buyer, particulars, amount).
/// </summary>
public sealed class CashBookImportParserTests
{
    private const string Header =
        "Date,Catagories,Particulars,Amount,Date,Catagories,Supplier,Buyer,Particulars,Amount";

    [Fact]
    public void A_row_with_both_sides_parses_into_two_independent_sides()
    {
        var parse = CashBookImportParser.Parse(
            Header + "\n"
            + "01-02-2025,Loan A/C Chairman,,\"261,080\",01-02-25,Subcontract bill,,Brooklyn: Joggers,,\"2,400\"");

        Assert.Empty(parse.Errors);
        Assert.Equal(2, parse.Sides.Count);

        var receipt = parse.Sides[0];
        Assert.Equal(CashTransactionKind.Receipt, receipt.Kind);
        Assert.Equal(new DateOnly(2025, 2, 1), receipt.TransactionDate);
        Assert.Equal("Loan A/C Chairman", receipt.Category);
        Assert.Equal(261_080m, receipt.Amount);

        var payment = parse.Sides[1];
        Assert.Equal(CashTransactionKind.Payment, payment.Kind);
        Assert.Equal(new DateOnly(2025, 2, 1), payment.TransactionDate);
        Assert.Equal("Subcontract bill", payment.Category);
        Assert.Equal("Brooklyn: Joggers", payment.Buyer);
        Assert.Equal(2_400m, payment.Amount);
    }

    [Fact]
    public void The_correctly_spelled_header_is_accepted_too()
    {
        var parse = CashBookImportParser.Parse(
            "Date,Categories,Particulars,Amount,Date,Categories,Supplier,Buyer,Particulars,Amount\n"
            + "01-02-2025,Loan A/C Chairman,,500,,,,,,");

        Assert.Empty(parse.Errors);
        Assert.Single(parse.Sides);
    }

    [Fact]
    public void A_half_empty_row_is_one_side_not_an_error()
    {
        var parse = CashBookImportParser.Parse(
            Header + "\n"
            + "01-02-2025,Received: Urbo ltd,inv 12,\"50,000\",,,,,,");

        Assert.Empty(parse.Errors);
        var side = Assert.Single(parse.Sides);
        Assert.Equal(CashTransactionKind.Receipt, side.Kind);
        Assert.Equal(50_000m, side.Amount);
    }

    [Fact]
    public void A_file_without_the_header_is_refused_not_guessed_at()
    {
        var parse = CashBookImportParser.Parse("some,other,file\n1,2,3,4,5,6,7,8,9,10");

        var error = Assert.Single(parse.Errors);
        Assert.Equal(CashBookImportErrorCode.FileHeaderMissing, error.Code);
        Assert.Empty(parse.Sides);
    }

    [Fact]
    public void A_short_row_is_refused_instead_of_silently_skipped()
    {
        var parse = CashBookImportParser.Parse(Header + "\n" + "01-02-2025,Loan A/C Chairman,,500");

        var error = Assert.Single(parse.Errors);
        Assert.Equal(2, error.LineNumber);
        Assert.Equal(CashBookImportErrorCode.RowTooShort, error.Code);
    }

    [Fact]
    public void A_side_with_any_content_must_be_complete()
    {
        // Payment half has a category and amount but no date.
        var parse = CashBookImportParser.Parse(
            Header + "\n"
            + ",,,,,Salary A/C,,,wages,\"2,400\"");

        var error = Assert.Single(parse.Errors);
        Assert.Equal(CashTransactionKind.Payment, error.Kind);
        Assert.Equal(CashBookImportErrorCode.DateUnparseable, error.Code);
        Assert.Empty(parse.Sides);
    }

    [Fact]
    public void A_blank_category_is_refused_because_the_account_would_be_unknown()
    {
        var parse = CashBookImportParser.Parse(Header + "\n" + "01-02-2025,,,500,,,,,,");

        var error = Assert.Single(parse.Errors);
        Assert.Equal(CashBookImportErrorCode.CategoryMissing, error.Code);
    }

    [Fact]
    public void A_non_numeric_amount_is_refused()
    {
        var parse = CashBookImportParser.Parse(Header + "\n" + "01-02-2025,Loan A/C Chairman,,lots,,,,,,");

        var error = Assert.Single(parse.Errors);
        Assert.Equal(CashBookImportErrorCode.AmountUnparseable, error.Code);
    }

    [Fact]
    public void A_zero_amount_is_refused_because_the_side_already_states_direction()
    {
        var parse = CashBookImportParser.Parse(Header + "\n" + "01-02-2025,Loan A/C Chairman,,\"0\",,,,,,");

        var error = Assert.Single(parse.Errors);
        Assert.Equal(CashBookImportErrorCode.AmountNotPositive, error.Code);
    }

    [Fact]
    public void Title_rows_above_the_header_and_blank_rows_below_are_skipped()
    {
        var parse = CashBookImportParser.Parse(
            "the client Garments\nCash Book — February\n\n"
            + Header + "\n\n"
            + "03-02-2025,Received: Adl,,300,,,,,,\n\n");

        Assert.Empty(parse.Errors);
        var side = Assert.Single(parse.Sides);
        Assert.Equal(6, side.LineNumber);
    }

    [Fact]
    public void Quoted_fields_keep_embedded_commas_and_escaped_quotes()
    {
        var parse = CashBookImportParser.Parse(
            Header + "\n"
            + "01-02-2025,Received: Urbo ltd,\"invoice \"\"12\"\", march\",\"1,000\",,,,,,");

        Assert.Empty(parse.Errors);
        var side = Assert.Single(parse.Sides);
        Assert.Equal("invoice \"12\", march", side.Particulars);
        Assert.Equal(1_000m, side.Amount);
    }
}
