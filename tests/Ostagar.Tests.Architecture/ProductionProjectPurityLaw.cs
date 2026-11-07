using System.Xml.Linq;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// L6 — test tooling never ships inside a production assembly. The legacy API project referenced
/// xunit, Moq, Microsoft.NET.Test.Sdk and Mvc.Testing while excluding its own test folder from
/// compilation, so it carried a test framework to production and ran none of its tests.
/// </summary>
internal sealed class ProductionProjectPurityLaw : EngineeringLaw
{
    private static readonly string[] BannedInProduction =
    [
        "xunit",
        "xunit.runner.visualstudio",
        "Microsoft.NET.Test.Sdk",
        "Moq",
        "FsCheck",
        "FsCheck.Xunit",
        "Microsoft.AspNetCore.Mvc.Testing",
        "Microsoft.EntityFrameworkCore.InMemory",
        "Testcontainers.PostgreSql",
        "coverlet.collector",
    ];

    public string Id => "L6";

    public string Rule => "No test-only package may be referenced by a project under src/.";

    public IEnumerable<Violation> FindViolations()
    {
        foreach (var path in SourceTree.ProductionProjectFiles())
        {
            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            var banned = document.Descendants("PackageReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(value => value is not null && BannedInProduction.Contains(value, StringComparer.OrdinalIgnoreCase))
                .ToList();

            foreach (var packageId in banned)
            {
                yield return new Violation(
                    SourceTree.Relative(path),
                    1,
                    Id,
                    $"'{packageId}' is a test-only package and must live in a test project, not in production.");
            }
        }
    }
}
