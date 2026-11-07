namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One reason a print was refused, with the position of the line that caused it where a line is to blame.
/// </summary>
/// <param name="Code">The rule that refused.</param>
/// <param name="Message">English sentence for the operator, who is usually standing at a printer with a
/// customer waiting and needs to know which line to look at.</param>
/// <param name="LinePosition">One-based line position, or null when the defect is in the header or the
/// totals. Same convention as the ledger's per-line posting errors.</param>
public sealed record PrintReadinessError(
    PrintReadinessErrorCode Code,
    string Message,
    int? LinePosition = null);
