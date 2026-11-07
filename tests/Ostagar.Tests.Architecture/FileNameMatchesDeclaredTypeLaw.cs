using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// L1b — a file is named after the one type it declares, so aggregate files cannot exist.
/// The legacy build kept <c>DTOs/JournalEntryDtos.cs</c> and <c>Models/Users/UserType.cs</c>
/// alongside per-type files, which is how "one class per file" quietly becomes "a drawer of types
/// per file". A plural category filename is the tell.
/// </summary>
internal sealed class FileNameMatchesDeclaredTypeLaw : EngineeringLaw
{
    private static readonly string[] AggregateStems =
    [
        "dtos", "dtos2", "models", "types", "enums", "interfaces", "classes", "entities",
        "requests", "responses", "common", "shared", "misc", "utils", "helpers", "constants",
        "globals", "core", "base",
    ];

    public string Id => "L1b";

    public string Rule => "File name must equal the single top-level type it declares; aggregate files like *Dtos.cs are banned.";

    public IEnumerable<Violation> FindViolations()
    {
        foreach (var path in SourceTree.AllSourceFiles())
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            var relative = SourceTree.Relative(path);

            var root = CSharpSyntaxTree.ParseText(SourceTree.ReadSource(path), path: path).GetRoot();
            var topLevelTypes = root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Where(type => !type.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any())
                .Select(type => type.Identifier.Text)
                .ToList();

            if (AggregateStems.Contains(stem, StringComparer.OrdinalIgnoreCase))
            {
                yield return new Violation(
                    relative,
                    1,
                    Id,
                    $"'{stem}.cs' is an aggregate filename. One type per file means one file per type, named after it.");
                continue;
            }

            if (topLevelTypes.Count == 0 || topLevelTypes.Contains(stem, StringComparer.Ordinal))
            {
                continue;
            }

            // EF Core names migration files `<14-digit timestamp>_<Name>.cs` and pairs each with a
            // `.Designer.cs`. Both files declare exactly one type, so they satisfy the law's intent;
            // the timestamp and the designer suffix are tooling convention, not a drawer of types.
            // Exempted narrowly — the whole Migrations folder is not given a free pass.
            var effectiveStem = Regex.Replace(stem, @"^\d{14}_", string.Empty);
            effectiveStem = Regex.Replace(effectiveStem, @"\.Designer$", string.Empty, RegexOptions.IgnoreCase);

            if (topLevelTypes.Contains(effectiveStem, StringComparer.Ordinal))
            {
                continue;
            }

            yield return new Violation(
                relative,
                1,
                Id,
                $"'{stem}.cs' declares '{topLevelTypes[0]}'. Name the file after the type it holds.");
        }
    }
}
