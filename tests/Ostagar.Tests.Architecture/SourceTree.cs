namespace Ostagar.Tests.Architecture;

/// <summary>
/// Locates and enumerates the source files the engineering-law rules run against.
/// </summary>
/// <remarks>
/// Enumeration is a method call, never a cached property. The first version of this class cached
/// the file list on first access, which made every law blind to files created afterwards and caused
/// all six firepower probes to fail for the wrong reason: a law that cannot see a new file cannot
/// see a newly introduced breach either.
/// </remarks>
internal static class SourceTree
{
    private static readonly string[] BuildArtifactDirs = ["obj", "bin", "node_modules", ".next", "legacy"];

    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Production sources only: the rules about DI, fake success and authorization.</summary>
    internal static IReadOnlyList<string> ProductionSourceFiles() => EnumerateSourceFiles("src");

    /// <summary>Everything we author, including tests: the file-shape rules apply to all of it.</summary>
    internal static IReadOnlyList<string> AllSourceFiles() => EnumerateSourceFiles("src", "tests");

    internal static IReadOnlyList<string> ProductionProjectFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Reads a source file with the BOM stripped, so byte-order marks never break a text rule.
    /// </summary>
    internal static string ReadSource(string path) => File.ReadAllText(path).TrimStart('﻿');

    /// <summary>Repository-relative path with forward slashes, for readable failure output.</summary>
    internal static string Relative(string path) =>
        Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

    private static bool IsBuildArtifact(string path)
    {
        var relative = Relative(path);
        return BuildArtifactDirs.Any(segment => relative.Contains($"/{segment}/", StringComparison.Ordinal))
            || relative.StartsWith("legacy/", StringComparison.Ordinal);
    }

    private static string[] EnumerateSourceFiles(params string[] roots)
    {
        var files = new List<string>();

        foreach (var rootName in roots)
        {
            var root = Path.Combine(RepositoryRoot, rootName);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var candidate in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (IsBuildArtifact(candidate) || candidate.EndsWith(".AssemblyInfo.cs", StringComparison.Ordinal))
                {
                    continue;
                }

                files.Add(candidate);
            }
        }

        return files.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var probe = new DirectoryInfo(AppContext.BaseDirectory);

        while (probe is not null)
        {
            if (File.Exists(Path.Combine(probe.FullName, "Ostagar.slnx")))
            {
                return probe.FullName;
            }

            probe = probe.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root above {AppContext.BaseDirectory}. Run tests from the Ostagar solution.");
    }
}
