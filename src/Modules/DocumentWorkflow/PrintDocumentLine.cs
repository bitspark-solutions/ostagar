using Ostagar.Core;

namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// One printed line of a document: its position, what it says in each language, and its amount.
/// </summary>
/// <remarks>
/// <see cref="DescriptionBangla"/> travels beside the English description rather than being looked up,
/// because a line on a printed order is the text that was agreed at the time. A buyer's "30 single jersey,
/// 180 gsm" has to print as the words the two parties read, and re-translating or dropping the Bangla form
/// at print time is how a paper document stops matching the conversation that produced it.
///
/// <see cref="Amount"/> is <see cref="Money"/>, never a bare decimal: a line whose currency is implied is
/// the line that let a USD figure into a taka invoice, which is the defect <see cref="Currency"/> exists
/// to make unwritable.
/// </remarks>
/// <param name="Position">One-based order on the paper. Carried rather than inferred from list order so a
/// line inserted later cannot silently renumber what a buyer already signed.</param>
/// <param name="Description">English line text.</param>
/// <param name="DescriptionBangla">Bangla line text, which may legitimately equal the English when the
/// text is an item code.</param>
/// <param name="Amount">The line's value in one named currency.</param>
public sealed record PrintDocumentLine(
    int Position,
    string Description,
    string DescriptionBangla,
    Money Amount);
