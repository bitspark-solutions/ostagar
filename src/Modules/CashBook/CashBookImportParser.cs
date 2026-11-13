using System.Globalization;

namespace Ostagar.Modules.CashBook;

/// <summary>
/// Reads the client's two-sided cash-book CSV into <see cref="CashBookImportSide"/>s.
/// </summary>
/// <remarks>
/// <para>
/// The layout is the pilot client's own export, recovered from the frozen predecessor's importer
/// (<c>legacy/.../CashBookImportController.cs</c>): ten columns, of which the first four are the
/// money-in half and the last six are the money-out half —
/// </para>
///
/// <code>
/// 0: credit date (dd-MM-yyyy) | 1: credit category | 2: credit particulars | 3: credit amount
/// 4: debit date (dd-MM-yy)    | 5: debit category  | 6: supplier           | 7: buyer
/// 8: debit particulars        | 9: debit amount
/// </code>
///
/// <para>
/// Amounts are printed with thousands separators inside quotes (<c>"261,080"</c>). The client's file
/// types the credit date with a four-digit year and the debit date with two; both spellings are
/// accepted on both sides so a reformatted export still parses, but a two-digit year below 2000 is
/// lifted by a century the same way the legacy importer did, because the client's books predate the
/// export tool that started writing 20xx.
/// </para>
///
/// <para>
/// Three rules differ from the legacy parser on purpose, all in the direction of refusing loudly:
/// a line shorter than ten columns is an error rather than a silent skip; a side that has any content
/// must have all of a date, a category and a positive amount rather than partially parsing; and a file
/// with no header row is refused entirely instead of guessing that data starts on line 3.
/// </para>
/// </remarks>
public static class CashBookImportParser
{
    /// <summary>
    /// The column the file declares itself by. The client's own header misspells it ("Catagories");
    /// both spellings count, because the typo is load-bearing — it is how the real files identify
    /// themselves.
    /// </summary>
    private static readonly string[] HeaderTokens = ["Date", "Amount"];

    private static readonly string[] CategoryHeaderSpellings = ["Catagories", "Categories"];

    /// <summary>
    /// Accepted date spellings, most specific first so a four-digit year is not claimed by the
    /// two-digit pattern.
    /// </summary>
    private static readonly string[] DateFormats =
    [
        "d-M-yyyy", "dd-MM-yyyy",
        "d/M/yyyy", "dd/MM/yyyy",
        "d-M-yy", "dd-MM-yy",
        "d/M/yy", "dd/MM/yy",
    ];

    private const int FieldCount = 10;

    private const int CreditDateIndex = 0;
    private const int CreditCategoryIndex = 1;
    private const int CreditParticularsIndex = 2;
    private const int CreditAmountIndex = 3;
    private const int DebitDateIndex = 4;
    private const int DebitCategoryIndex = 5;
    private const int DebitSupplierIndex = 6;
    private const int DebitBuyerIndex = 7;
    private const int DebitParticularsIndex = 8;
    private const int DebitAmountIndex = 9;

    /// <summary>The century boundary for two-digit years, matching the legacy importer's fix-up.</summary>
    private const int TwoDigitYearPivot = 2000;
    private const int Century = 100;

    /// <summary>
    /// Parses the whole file. Rows between the header and the first data row, and blank filler lines,
    /// are skipped — the book prints spacer rows and they are not transactions.
    /// </summary>
    public static CashBookImportParse Parse(string content)
    {
        var sides = new List<CashBookImportSide>();
        var errors = new List<CashBookImportError>();
        var lines = content.Split('\n');

        var dataStart = FindDataStart(lines);
        if (dataStart is null)
        {
            errors.Add(new CashBookImportError(
                0, null, CashBookImportErrorCode.FileHeaderMissing,
                "No cash-book header row was found; this does not look like the client's export."));

            return new CashBookImportParse { Sides = sides, Errors = errors };
        }

        for (var index = dataStart.Value; index < lines.Length; index++)
        {
            var text = lines[index].Trim('\r', ' ', '\t');
            if (text.Length == 0)
            {
                continue;
            }

            var lineNumber = index + 1;
            var fields = CashBookCsv.ParseLine(text);

            if (fields.Count < FieldCount)
            {
                errors.Add(new CashBookImportError(
                    lineNumber, null, CashBookImportErrorCode.RowTooShort,
                    $"The row has {fields.Count} column(s); the cash-book layout needs {FieldCount}."));
                continue;
            }

            ParseSide(
                fields,
                lineNumber,
                kind: CashTransactionKind.Receipt,
                dateIndex: CreditDateIndex,
                categoryIndex: CreditCategoryIndex,
                particularsIndex: CreditParticularsIndex,
                amountIndex: CreditAmountIndex,
                supplierIndex: null,
                buyerIndex: null,
                sides,
                errors);

            ParseSide(
                fields,
                lineNumber,
                kind: CashTransactionKind.Payment,
                dateIndex: DebitDateIndex,
                categoryIndex: DebitCategoryIndex,
                particularsIndex: DebitParticularsIndex,
                amountIndex: DebitAmountIndex,
                supplierIndex: DebitSupplierIndex,
                buyerIndex: DebitBuyerIndex,
                sides,
                errors);
        }

        return new CashBookImportParse { Sides = sides, Errors = errors };
    }

    /// <summary>
    /// The line after the header, or null when no header exists. The header is recognised by holding a
    /// date column, an amount column and a category column under either spelling — the shape the
    /// client's export prints above its data, with title rows above that.
    /// </summary>
    private static int? FindDataStart(string[] lines)
    {
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (HeaderTokens.All(line.Contains)
                && CategoryHeaderSpellings.Any(line.Contains))
            {
                return index + 1;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads one half of a row. A side with no content at all is simply absent — the two halves are
    /// independent columns and most rows fill only one. A side with any content must be complete.
    /// </summary>
    private static void ParseSide(
        IReadOnlyList<string> fields,
        int lineNumber,
        CashTransactionKind kind,
        int dateIndex,
        int categoryIndex,
        int particularsIndex,
        int amountIndex,
        int? supplierIndex,
        int? buyerIndex,
        List<CashBookImportSide> sides,
        List<CashBookImportError> errors)
    {
        var dateText = fields[dateIndex];
        var category = fields[categoryIndex];
        var particulars = fields[particularsIndex];
        var amountText = fields[amountIndex];

        var present = dateText.Length > 0 || category.Length > 0 || amountText.Length > 0
            || (supplierIndex is int s && fields[s].Length > 0)
            || (buyerIndex is int b && fields[b].Length > 0);

        if (!present)
        {
            return;
        }

        var sideErrors = new List<CashBookImportError>();

        var date = ParseDate(dateText);
        if (date is null)
        {
            sideErrors.Add(new CashBookImportError(
                lineNumber, kind, CashBookImportErrorCode.DateUnparseable,
                $"The date '{dateText}' is not readable; the book writes dd-MM-yyyy."));
        }

        if (category.Length == 0)
        {
            sideErrors.Add(new CashBookImportError(
                lineNumber, kind, CashBookImportErrorCode.CategoryMissing,
                "The category is blank, so the account the movement was for is unknown."));
        }

        var amount = ParseAmount(amountText);
        if (amount is null)
        {
            sideErrors.Add(new CashBookImportError(
                lineNumber, kind, CashBookImportErrorCode.AmountUnparseable,
                $"The amount '{amountText}' is not a number."));
        }
        else if (amount <= 0)
        {
            sideErrors.Add(new CashBookImportError(
                lineNumber, kind, CashBookImportErrorCode.AmountNotPositive,
                "The amount must be positive; the side of the page already states the direction."));
        }

        if (sideErrors.Count > 0)
        {
            errors.AddRange(sideErrors);
            return;
        }

        sides.Add(new CashBookImportSide
        {
            Kind = kind,
            LineNumber = lineNumber,
            TransactionDate = date!.Value,
            Category = category,
            Amount = amount!.Value,
            Particulars = particulars,
            Supplier = supplierIndex is int si ? fields[si] : string.Empty,
            Buyer = buyerIndex is int bi ? fields[bi] : string.Empty,
        });
    }

    /// <summary>
    /// Reads a book date. Two-digit years below the pivot are lifted a century: the client's early
    /// books write "01-02-25" meaning 2025, and some exports render it as 1925 — the fix-up the legacy
    /// importer applied, kept because the files it was written for are the files being imported.
    /// </summary>
    private static DateOnly? ParseDate(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return null;
        }

        var year = parsed.Year < TwoDigitYearPivot ? parsed.Year + Century : parsed.Year;

        return new DateOnly(year, parsed.Month, parsed.Day);
    }

    /// <summary>Reads an amount printed with thousands separators and optional quoting.</summary>
    private static decimal? ParseAmount(string text)
    {
        var cleaned = text.Replace(",", string.Empty, StringComparison.Ordinal).Trim('"', ' ');

        return cleaned.Length > 0
            && decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                ? amount
                : null;
    }
}
