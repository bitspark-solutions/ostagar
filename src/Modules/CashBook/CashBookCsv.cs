namespace Ostagar.Modules.CashBook;

/// <summary>
/// The lexical half of CSV: turning one physical line into its fields, nothing more.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="CashBookImportParser"/>, which decides what the fields mean. The legacy
/// importer inlined this loop inside the controller and got two of the three rules wrong — it split on
/// commas inside quoted amounts like <c>"261,080"</c> correctly but treated a doubled quote
/// (<c>""</c>) as two delimiters, so a particulars field containing a quote silently ate the rest of
/// the line. This version handles RFC 4180 quoting exactly and knows nothing about cash books.
/// </remarks>
public static class CashBookCsv
{
    /// <summary>
    /// Splits one line into fields. Quoted fields may contain commas; a quote inside a quoted field is
    /// written as two quotes. The client's export does not emit embedded newlines, so a line is a row —
    /// a format that did emit them would need a record reader, and that is a deliberate non-goal.
    /// </summary>
    public static IReadOnlyList<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var c = line[index];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // A doubled quote is an escaped quote; a lone one closes the field.
                    if (index + 1 < line.Length && line[index + 1] == '"')
                    {
                        current.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }
}
