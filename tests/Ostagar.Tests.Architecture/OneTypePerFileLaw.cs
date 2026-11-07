using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// L1 — exactly one top-level type per file, and no smuggling a second type inside the first.
/// The owner's standing rule, stated as "A MAJOR MUST": one file each for classes, records,
/// interfaces, enums and DTOs, in C# and TypeScript alike.
/// Two enums sharing a file breaches it exactly as two classes do, and a public nested type is
/// only a workaround for the same rule — so both are checked here.
/// </summary>
internal sealed class OneTypePerFileLaw : EngineeringLaw
{
    public string Id => "L1";

    public string Rule => "One top-level class/interface/enum/record per file, and no nested public or enum type bundled inside it.";

    public IEnumerable<Violation> FindViolations()
    {
        foreach (var path in SourceTree.AllSourceFiles())
        {
            var relative = SourceTree.Relative(path);
            var root = CSharpSyntaxTree.ParseText(SourceTree.ReadSource(path), path: path).GetRoot();

            var declaredTypes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().ToList();

            var topLevelTypes = declaredTypes
                .Where(type => !type.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any())
                .ToList();

            for (var index = 1; index < topLevelTypes.Count; index++)
            {
                var extra = topLevelTypes[index];
                yield return new Violation(
                    relative,
                    extra.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Id,
                    $"File declares {topLevelTypes.Count} top-level types; '{extra.Identifier.Text}' ({Describe(extra)}) must move to its own file.");
            }

            foreach (var bundled in declaredTypes.Where(type => type.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any()))
            {
                var isEnum = bundled is EnumDeclarationSyntax;
                var isPublic = bundled.Modifiers.Any(SyntaxKind.PublicKeyword)
                    || bundled.Modifiers.Any(SyntaxKind.InternalKeyword)
                    || bundled.Modifiers.Any(SyntaxKind.ProtectedKeyword);

                if (!isEnum && !isPublic)
                {
                    continue;
                }

                var parent = bundled.Ancestors().OfType<BaseTypeDeclarationSyntax>().First();

                yield return new Violation(
                    relative,
                    bundled.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Id,
                    isEnum
                        ? $"'{bundled.Identifier.Text}' is an enum nested inside '{parent.Identifier.Text}'. Every enum is its own domain vocabulary and gets its own file."
                        : $"'{bundled.Identifier.Text}' is a visible type nested inside '{parent.Identifier.Text}'. Nesting here bundles a second type into one file — give it its own file.");
            }
        }
    }

    private static string Describe(BaseTypeDeclarationSyntax declaration) => declaration switch
    {
        RecordDeclarationSyntax _ => "record",
        ClassDeclarationSyntax _ => "class",
        StructDeclarationSyntax _ => "struct",
        InterfaceDeclarationSyntax _ => "interface",
        EnumDeclarationSyntax _ => "enum",
        _ => "type",
    };
}
