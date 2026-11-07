using System.Text.RegularExpressions;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// L2 — no commented-out registrations. The legacy build shipped 22 of 35 service registrations
/// as comments in Program.cs, so controllers compiled cleanly and then threw at activation.
/// A commented registration is now a build failure, not a note to deal with later.
/// </summary>
internal sealed partial class NoCommentedOutRegistrationLaw : EngineeringLaw
{
    public string Id => "L2";

    public string Rule => "No commented-out DI registrations; the composition root is complete or the build fails.";

    public IEnumerable<Violation> FindViolations()
    {
        foreach (var path in SourceTree.ProductionSourceFiles())
        {
            var relative = SourceTree.Relative(path);
            var lines = SourceTree.ReadSource(path).Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].TrimStart();

                // XML documentation is prose, not disabled code. Without this guard the rule fires on
                // any doc comment that merely mentions AddDbContext<T> while explaining why pooling is
                // unsafe — a law with false positives gets silenced rather than obeyed.
                if (trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var comment = trimmed[2..];
                if (RegistrationPattern().IsMatch(comment))
                {
                    yield return new Violation(
                        relative,
                        index + 1,
                        Id,
                        "Commented-out service registration. Register it, remove the service, or remove the controller that needs it — never leave it disabled.");
                }

                if (DisabledSeedingPattern().IsMatch(comment))
                {
                    yield return new Violation(
                        relative,
                        index + 1,
                        Id,
                        "Commented-out seeding or schema step. Seeded permissions and reference data are security-relevant; wire them or delete them.");
                }
            }
        }
    }

    [GeneratedRegex(@"\bAdd(Scoped|Singleton|Transient|HostedService|DbContext)\s*[<(]")]
    private static partial Regex RegistrationPattern();

    [GeneratedRegex(@"(Seed\w*|MigrateAsync|\.Migrate\(|UseMiddleware<)")]
    private static partial Regex DisabledSeedingPattern();
}
