using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// Whether a document may be printed, and the exact figures and label the print must use.
/// </summary>
/// <remarks>
/// <see cref="GrandTotal"/>, <see cref="DueTotal"/> and <see cref="StatusLabel"/> are populated only when
/// the print is safe, and they are the document's <em>stored</em> values echoed back rather than results
/// computed here. That echo is the contract with the renderer: it is given the numbers to print instead of
/// the lines to add up, so a screen, a PDF and a reprint of a three-year-old invoice cannot produce three
/// totals from one set of rows. When the check fails the renderer gets nothing to print, which is the
/// correct answer — a document whose own two accounts of its total disagree must not leave the building.
/// </remarks>
/// <param name="IsSafe">Every rule passed.</param>
/// <param name="Errors">Every defect found, reported together so the operator fixes the document in one pass.</param>
/// <param name="GrandTotal">The stored total to print.</param>
/// <param name="DueTotal">The stored balance due to print.</param>
/// <param name="StatusLabel">Both printed names of the document's state.</param>
public sealed record PrintReadinessDecision(
    bool IsSafe,
    IReadOnlyList<PrintReadinessError> Errors,
    Money? GrandTotal = null,
    Money? DueTotal = null,
    BilingualLabel? StatusLabel = null);
