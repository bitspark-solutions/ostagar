using System.Collections.ObjectModel;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// Proves every registered law can actually fail.
/// </summary>
/// <remarks>
/// This exists because the legacy repository's checks had never been seen to fail: a test folder
/// excluded from compilation, a validator that was never registered, an invalidation stub that
/// logged and returned. A rule nobody has watched fire is indistinguishable from a rule that does
/// nothing. Each probe is planted under <c>src/</c>, the laws are re-run, and the plant is removed
/// whether the assertions passed or not.
/// </remarks>
public class LawFirepowerTests
{
    private static readonly ReadOnlyDictionary<string, string> ProbeFiles = new(new Dictionary<string, string>
    {
        ["L1"] = """
            namespace Ostagar.LawProbe;

            public enum ProbeKind { Alpha, Beta }

            public enum ProbeAvailability { Confirmed, Unknown }
            """,

        ["L1b"] = """
            namespace Ostagar.LawProbe;

            public sealed class TotallyDifferentName
            {
            }
            """,

        ["L2"] = """
            namespace Ostagar.LawProbe;

            // builder.Services.AddScoped<IProbeDependency, ProbeDependency>();
            internal static class ProbeComposition { }
            """,

        ["L3"] = """
            using Microsoft.AspNetCore.Mvc;

            namespace Ostagar.LawProbe;

            public class ProbeController : ControllerBase
            {
                [HttpGet("probe")]
                public IActionResult Read() => Ok(new { journalEntryId = System.Guid.NewGuid() });
            }
            """,

        ["L6"] = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.9.3" />
              </ItemGroup>
            </Project>
            """,

        ["L8"] = """
            using Microsoft.AspNetCore.Mvc;

            namespace Ostagar.LawProbe;

            public class ProbeOpenController : ControllerBase
            {
                [HttpGet("probe-open")]
                public IActionResult Read() => Ok();
            }
            """,

        ["L18"] = """
            using Microsoft.EntityFrameworkCore;

            namespace Ostagar.LawProbe;

            public sealed class ProbeL18
            {
                public DbSet<object>? Rows { get; set; }
            }
            """,
    });

    [Theory]
    [MemberData(nameof(LawIdsWithProbes))]
    public void Law_fires_on_a_known_bad_sample(string lawId)
    {
        var law = EngineeringLaws.ById(lawId);
        var planted = Plant();

        try
        {
            var violations = law.FindViolations().ToList();

            Assert.NotEmpty(violations);
            Assert.Contains(violations, violation => violation.FilePath.Contains("Ostagar.LawProbe", StringComparison.Ordinal));
        }
        finally
        {
            foreach (var path in planted)
            {
                TryDelete(path);
            }

            TryRemoveProbeDirectory();
        }
    }

    /// <summary>
    /// A law that fires on compliant code gets switched off rather than obeyed, so firing is only
    /// half the proof. This plants legal prose that mentions the banned shapes and requires silence.
    /// It is not hypothetical: L2 originally reported every XML doc comment containing
    /// <c>AddDbContextPool</c> as a commented-out registration.
    /// </summary>
    [Fact]
    public void Laws_stay_silent_on_compliant_prose_that_names_the_banned_shapes()
    {
        var root = Path.Combine(SourceTree.RepositoryRoot, "src", "Ostagar.LawProbe");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "CompliantProse.cs");

        File.WriteAllText(path, """
            namespace Ostagar.LawProbe;

            /// <summary>
            /// Paired with <c>AddDbContext</c> and never <c>AddDbContextPool</c>, because pooling reuses
            /// tracked state. We considered <c>AddSingleton&lt;IProbe, Probe&gt;</c> and rejected it, and
            /// deliberately do not call <c>SeedPermissionsAsync</c> at startup.
            /// </summary>
            internal static class CompliantProse
            {
            }
            """);

        try
        {
            foreach (var law in EngineeringLaws.All)
            {
                var hits = law.FindViolations()
                    .Where(violation => violation.FilePath.Contains("CompliantProse", StringComparison.Ordinal))
                    .ToList();

                Assert.Empty(hits);
            }
        }
        finally
        {
            TryDelete(path);
            TryRemoveProbeDirectory();
        }
    }

    public static TheoryData<string> LawIdsWithProbes()
    {
        var data = new TheoryData<string>();
        foreach (var law in EngineeringLaws.All)
        {
            data.Add(law.Id);
        }

        return data;
    }

    /// <summary>
    /// Drops the whole probe set for every run: a law is only credible if it is shown to react to
    /// the same planted state a colleague would hit, not only to its own fixture.
    /// </summary>
    private static List<string> Plant()
    {
        var root = Path.Combine(SourceTree.RepositoryRoot, "src", "Ostagar.LawProbe");
        Directory.CreateDirectory(root);

        var paths = new List<string>();

        foreach (var (lawId, content) in ProbeFiles)
        {
            var name = lawId == "L6" ? "Ostagar.LawProbe.csproj" : $"Probe{lawId.Replace("b", "B")}.cs";
            var path = Path.Combine(root, name);
            File.WriteAllText(path, content);
            paths.Add(path);
        }

        return paths;
    }

    /// <summary>
    /// Removes the probe directory once it is empty, so a passing run leaves no residue for the
    /// next reader to wonder about.
    /// </summary>
    private static void TryRemoveProbeDirectory()
    {
        var root = Path.Combine(SourceTree.RepositoryRoot, "src", "Ostagar.LawProbe");

        try
        {
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            {
                return;
            }

            Directory.Delete(root);
        }
        catch (IOException)
        {
            // Left behind and gitignored; the next run retries.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A locked probe is reported by the next build's coverage counts, not swallowed here.
        }
    }
}
