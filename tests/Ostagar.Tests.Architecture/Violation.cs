namespace Ostagar.Tests.Architecture;

/// <summary>
/// A single engineering-law breach found in a source file.
/// </summary>
/// <param name="FilePath">Repository-relative path, forward slashes.</param>
/// <param name="Line">1-based line number of the offending declaration.</param>
/// <param name="Law">The plan law identifier, e.g. <c>L1</c>.</param>
/// <param name="Detail">What was found and what to do about it.</param>
internal readonly record struct Violation(string FilePath, int Line, string Law, string Detail)
{
    public override string ToString() => $"{Law} — {FilePath}:{Line} — {Detail}";
}
