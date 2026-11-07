using System.Xml.Linq;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// L18 — persistence knowledge lives in exactly one project. Domain projects may not reference the
/// data layer and may not name Entity Framework at all.
/// </summary>
/// <remarks>
/// <para>The reason this is a law rather than a convention: the fusion it prevents is invisible in a
/// single file. A domain entity gains <c>using Microsoft.EntityFrameworkCore</c> to get a navigation
/// attribute, the entity project starts referencing the context, the context maps entities from that
/// project, and the cycle is closed by the time anyone notices — at which point the only way out is a
/// refactor of every file on both sides. That is the state this repository was in when the split was
/// made, and it is recorded in docs/MASTER_PLAN.md §2.7.</para>
///
/// <para>
/// The exemption list is the layering, stated as paths. Hosts are exempt because a composition root
/// must be able to register a context; a domain project has no such excuse.
/// </para>
/// </remarks>
internal sealed class PersistenceBelongsToOneProjectLaw : EngineeringLaw
{
    private static readonly string[] AllowedProjectPrefixes = ["src/Data/", "src/Api/", "src/Cli/"];

    private static readonly string[] PersistenceUsingDirectives =
    [
        "using Microsoft.EntityFrameworkCore",
        "using Npgsql",
        "using Microsoft.AspNetCore.Identity.EntityFrameworkCore",
    ];

    public string Id => "L18";

    public string Rule =>
        "Only src/Data may know persistence: no domain project may reference Ostagar.Data, "
        + "the Entity Framework packages, or an EF using directive.";

    public IEnumerable<Violation> FindViolations()
    {
        foreach (var path in SourceTree.ProductionSourceFiles())
        {
            var relative = SourceTree.Relative(path);
            if (IsAllowed(relative))
            {
                continue;
            }

            var lines = SourceTree.ReadSource(path).Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();

                // Only an actual directive counts. Prose in a doc comment that names AddDbContext or
                // explains why the data layer exists is how this codebase documents itself, and a law
                // that punishes it gets switched off instead of obeyed.
                if (!PersistenceUsingDirectives.Any(line.StartsWith))
                {
                    continue;
                }

                yield return new Violation(
                    relative,
                    index + 1,
                    Id,
                    "A domain project is naming persistence directly. Map it in src/Data instead.");
            }
        }

        foreach (var path in SourceTree.ProductionProjectFiles())
        {
            var relative = SourceTree.Relative(path);
            if (IsAllowed(relative))
            {
                continue;
            }

            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }

            foreach (var reference in document.Descendants("ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value;

                if (include is not null && include.Replace('\\', '/').Contains("Ostagar.Data.csproj", StringComparison.Ordinal))
                {
                    yield return new Violation(
                        relative,
                        1,
                        Id,
                        "A domain project references the data layer, which is how the entity/persistence cycle returns.");
                }
            }

            foreach (var package in document.Descendants("PackageReference"))
            {
                var packageId = package.Attribute("Include")?.Value;

                if (packageId is not null
                    && (packageId.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                        || packageId.Equals("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return new Violation(
                        relative,
                        1,
                        Id,
                        $"'{packageId}' belongs to src/Data. A domain project that takes an EF package starts needing the context.");
                }
            }
        }
    }

    private static bool IsAllowed(string relativePath) =>
        AllowedProjectPrefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.Ordinal));
}
