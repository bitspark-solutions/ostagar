namespace Ostagar.Modules.DocumentWorkflow;

/// <summary>
/// The Bangla and English names of the period options on the numbering settings screen.
/// </summary>
/// <remarks>
/// Lives with the workflow module rather than in the web app because the pair is part of the rule, not
/// part of a page: a tenant who cannot read what "Fiscal month" will do to their invoice numbers has to
/// be able to read it in the language they work in, and a second copy of those words in a translation
/// file is the second definition that eventually disagrees with the first.
/// </remarks>
public static class NumberingPeriodComponentLabels
{
    /// <summary>The label pair for one period component.</summary>
    public static BilingualLabel LabelOf(NumberingPeriodComponent component) => component switch
    {
        NumberingPeriodComponent.None => new BilingualLabel("No period, one continuing series", "কোনো কাল নেই, একটাই ক্রম"),
        NumberingPeriodComponent.FiscalYear => new BilingualLabel("Fiscal year", "আর্থিক বছর"),
        NumberingPeriodComponent.FiscalMonth => new BilingualLabel("Fiscal month", "আর্থিক মাস"),
        _ => new BilingualLabel("Unknown period", "অজানা কাল"),
    };
}
