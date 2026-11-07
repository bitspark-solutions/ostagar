namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The machine code of one business document type — a sales order, a vendor bill, a payroll run.
/// </summary>
/// <remarks>
/// Numbering schemes, workflow definitions and print bindings are all keyed on this, so it has to be
/// one comparable value rather than the free-text <c>string</c> the legacy tree passed between
/// services. That looseness is exactly how three screens ended up with three notions of the same
/// document: <c>"Invoice"</c>, <c>"invoice"</c> and <c>"SALES INVOICE"</c> in one database, each with
/// its own numbering rule and its own idea of whether the document was editable. A typed code with a
/// fixed alphabet makes those three one value and makes a fourth spelling fail at the boundary.
///
/// Deliberately open-ended rather than an enum: a tenant adding a new document type adds a
/// <see cref="DocumentWorkflowDefinition"/>, and that must not require a code change here.
/// </remarks>
public readonly record struct DocumentTypeCode
{
    private readonly string _value;

    private DocumentTypeCode(string value) => _value = value;

    public static DocumentTypeCode SalesOrder { get; } = new("SALES_ORDER");

    public static DocumentTypeCode SalesInvoice { get; } = new("SALES_INVOICE");

    public static DocumentTypeCode CreditNote { get; } = new("CREDIT_NOTE");

    public static DocumentTypeCode PurchaseOrder { get; } = new("PURCHASE_ORDER");

    public static DocumentTypeCode PurchaseBill { get; } = new("PURCHASE_BILL");

    /// <summary>The vendor-side mirror of a credit note, and the correction of a posted vendor bill.</summary>
    public static DocumentTypeCode DebitNote { get; } = new("DEBIT_NOTE");

    public static DocumentTypeCode DeliveryNote { get; } = new("DELIVERY_NOTE");

    public static DocumentTypeCode WorkOrder { get; } = new("WORK_ORDER");

    public static DocumentTypeCode PayrollRun { get; } = new("PAYROLL_RUN");

    public static DocumentTypeCode StockMove { get; } = new("STOCK_MOVE");

    /// <summary>Upper-case machine code as stored and as it appears in an allocation key.</summary>
    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

    /// <summary>
    /// Parses and normalises a code, refusing anything that could be the same document type written
    /// two ways.
    /// </summary>
    public static DocumentTypeCode From(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A document type code is required.", nameof(code));
        }

        var normalised = code.Trim().ToUpperInvariant();

        if (normalised.Length is < 1 or > 32)
        {
            throw new ArgumentException(
                $"Document type code '{normalised}' must be between 1 and 32 characters.",
                nameof(code));
        }

        foreach (var character in normalised)
        {
            if (!char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                throw new ArgumentException(
                    $"Document type code '{code}' may only contain A-Z, 0-9 and underscore, so that one "
                    + "document type cannot exist under two spellings.",
                    nameof(code));
            }
        }

        return new DocumentTypeCode(normalised);
    }

    public static bool TryFrom(string? code, out DocumentTypeCode result)
    {
        try
        {
            result = From(code ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            result = default;
            return false;
        }
    }

    public override string ToString() => Value;
}
