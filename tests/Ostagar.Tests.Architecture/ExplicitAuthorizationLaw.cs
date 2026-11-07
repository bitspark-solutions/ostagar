using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// L8 — authorisation is explicit, deny by default. The legacy build called a bare
/// <c>AddAuthorization()</c> with no fallback policy, which left an admin-minting endpoint and a
/// mock financial endpoint reachable without credentials. Every action must carry a policy or
/// appear in a reviewed allow-list.
/// </summary>
internal sealed class ExplicitAuthorizationLaw : EngineeringLaw
{
    private const string AllowListFileName = "anonymous-allowlist.txt";

    public string Id => "L8";

    public string Rule => "Every controller action declares an authorisation policy, or is listed in anonymous-allowlist.txt.";

    public IEnumerable<Violation> FindViolations()
    {
        var allowListed = ReadAllowList();
        foreach (var path in SourceTree.ProductionSourceFiles())
        {
            var root = CSharpSyntaxTree.ParseText(SourceTree.ReadSource(path), path: path).GetRoot();

            foreach (var controller in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                         .Where(IsController))
            {
                var controllerName = controller.Identifier.Text.Replace("Controller", string.Empty, StringComparison.Ordinal);
                var classHasPolicy = HasAny(controller.AttributeLists, "Authorize", "AllowAnonymous", "AllowAnonymousWithReason");

                foreach (var action in controller.Members.OfType<MethodDeclarationSyntax>()
                             .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword))
                             .Where(method => HasAny(method.AttributeLists, "Http", "HttpGet", "HttpPost", "HttpPut", "HttpDelete", "Patch")))
                {
                    var actionName = $"{controllerName}/{action.Identifier.Text}";
                    var declared = classHasPolicy || HasAny(action.AttributeLists, "Authorize", "AllowAnonymous", "AuthorizeRole", "RequirePermission");

                    if (declared)
                    {
                        continue;
                    }

                    if (allowListed.Contains(actionName, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    yield return new Violation(
                        SourceTree.Relative(path),
                        action.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        Id,
                        $"'{actionName}' has no authorisation. Add [Authorize], or list it in {AllowListFileName} and expect that diff to be reviewed.");
                }
            }
        }
    }

    private static HashSet<string> ReadAllowList()
    {
        var path = Path.Combine(SourceTree.RepositoryRoot, AllowListFileName);
        if (!File.Exists(path))
        {
            return [];
        }

        return File.ReadLines(path)
            .Select(line => line.Split('#')[0].Trim())
            .Where(entry => entry.Length > 0 && entry.Contains('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsController(ClassDeclarationSyntax declaration) =>
        declaration.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal)
        || declaration.AttributeLists.SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name.ToString().Contains("Route", StringComparison.Ordinal));

    private static bool HasAny(SyntaxList<AttributeListSyntax> lists, params string[] names) =>
        lists.SelectMany(list => list.Attributes)
            .Any(attribute => names.Any(name => attribute.Name.ToString().Contains(name, StringComparison.Ordinal)));
}
