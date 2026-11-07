using System.Globalization;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// Runs every registered engineering law over the repository. A failure lists the breaches with
/// file and line so the fix is mechanical rather than another investigation.
/// </summary>
public class EngineeringLawTests
{
    [Theory]
    [MemberData(nameof(LawIds))]
    public void Law_holds_across_the_repository(string lawId)
    {
        var law = EngineeringLaws.ById(lawId);
        var violations = law.FindViolations().ToList();

        Assert.True(violations.Count == 0, FormatFailure(law, violations));
    }

    public static TheoryData<string> LawIds()
    {
        var data = new TheoryData<string>();
        foreach (var law in EngineeringLaws.All)
        {
            data.Add(law.Id);
        }

        return data;
    }

    private static string FormatFailure(EngineeringLaw law, List<Violation> violations)
    {
        var lines = new List<string>
        {
            string.Empty,
            $"{law.Id} — {law.Rule}",
            $"{violations.Count} violation{(violations.Count == 1 ? string.Empty : "s")}:",
        };

        lines.AddRange(violations.Take(50).Select(violation => violation.ToString()));

        if (violations.Count > 50)
        {
            lines.Add(string.Format(CultureInfo.InvariantCulture, "... and {0} more.", violations.Count - 50));
        }

        return string.Join(Environment.NewLine, lines);
    }
}
