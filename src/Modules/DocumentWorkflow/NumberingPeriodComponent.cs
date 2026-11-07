namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Which part of the tenant's calendar splits one document-type sequence into several: none, one
/// sequence per fiscal year, or one per fiscal month.
/// </summary>
/// <remarks>
/// The period component is part of the number's identity, not a display choice. A buyer's accountant
/// says "invoice 00412 of last year", and a VAT officer asks whether the numbers between two invoices
/// are missing. Both questions are only answerable if the sequence they refer to has a bounded,
/// named span, and changing that span mid-life re-opens spans that were already closed — which is how
/// a document type ends up with two live documents sharing one number. So the component is frozen
/// once the scheme has issued anything.
/// </remarks>
public enum NumberingPeriodComponent
{
    /// <summary>One sequence that never restarts. Used for legally serial vouchers.</summary>
    None,

    /// <summary>Restarts each fiscal year, which is the numbering span the Bangladeshi VAT file expects.</summary>
    FiscalYear,

    /// <summary>Restarts each fiscal month, for high-volume documents such as delivery notes.</summary>
    FiscalMonth,
}
