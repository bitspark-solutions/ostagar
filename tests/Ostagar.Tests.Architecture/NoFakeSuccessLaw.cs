using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ostagar.Tests.Architecture;

/// <summary>
/// L3 — no fake success. The legacy build shipped cash-book endpoints that returned
/// <c>journalEntryId = Guid.NewGuid()</c> and logged success while writing nothing, and
/// <c>NotImplementedException</c> behind routes that answered 200. A client must never be told a
/// financial document was saved unless a row was created.
/// </summary>
/// <remarks>
/// What this catches: a routed method handing back a freshly minted identifier, or answering with
/// NotImplementedException. What it cannot catch is a method that mints an id AND saves a row under
/// it — which is legitimate — so this rule is a tripwire, not proof. Real proof of "did it persist"
/// is the per-endpoint integration assertion required at each phase gate; a green static scan here
/// must never be read as evidence that a write endpoint works.
/// </remarks>
internal sealed class NoFakeSuccessLaw : EngineeringLaw
{
    private static readonly string[] FabricatedIdProperties =
    [
        "journalEntryId", "transactionId", "invoiceId", "orderId", "entryId", "documentId", "paymentId",
    ];

    public string Id => "L3";

    public string Rule => "No endpoint may report success it did not earn: no fabricated ids, no NotImplementedException behind a 200.";

    public IEnumerable<Violation> FindViolations()
    {
        foreach (var path in SourceTree.ProductionSourceFiles())
        {
            var relative = SourceTree.Relative(path);
            var root = CSharpSyntaxTree.ParseText(SourceTree.ReadSource(path), path: path).GetRoot();

            foreach (var throwStatement in root.DescendantNodes().OfType<ThrowStatementSyntax>())
            {
                if (throwStatement.Expression is ObjectCreationExpressionSyntax created
                    && created.Type.ToString().Contains("NotImplemented", StringComparison.Ordinal))
                {
                    var containingRoute = AnnotatedRouteMethod(throwStatement);
                    if (containingRoute is not null)
                    {
                        yield return new Violation(
                            relative,
                            throwStatement.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            Id,
                            "NotImplementedException inside a routed action: the caller gets an unhandled failure or a fake response, never a real one.");
                    }
                }
            }

            foreach (var anonymous in root.DescendantNodes().OfType<AnonymousObjectCreationExpressionSyntax>())
            {
                foreach (var initializer in anonymous.Initializers)
                {
                    var propertyName = initializer.NameEquals?.Name.ToString() ?? string.Empty;

                    if (IsFabricatedId(propertyName, initializer.Expression.ToString())
                        && AnnotatedRouteMethod(anonymous) is not null)
                    {
                        yield return Fabrication(relative, initializer, propertyName);
                    }
                }
            }

            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                foreach (var expression in creation.Initializer?.Expressions ?? Enumerable.Empty<ExpressionSyntax>())
                {
                    if (expression is not AssignmentExpressionSyntax memberAssignment)
                    {
                        continue;
                    }

                    var propertyName = LastSegment(memberAssignment.Left.ToString());

                    if (IsFabricatedId(propertyName, memberAssignment.Right.ToString())
                        && AnnotatedRouteMethod(creation) is not null)
                    {
                        yield return Fabrication(relative, memberAssignment, propertyName);
                    }
                }
            }

            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                // Initializer forms above already report this node; only plain statements land here.
                if (assignment.Parent is AnonymousObjectMemberDeclaratorSyntax
                    || assignment.Parent is InitializerExpressionSyntax
                    || assignment.Parent is EqualsValueClauseSyntax)
                {
                    continue;
                }

                var propertyName = LastSegment(assignment.Left.ToString());

                if (IsFabricatedId(propertyName, assignment.Right.ToString())
                    && AnnotatedRouteMethod(assignment) is not null)
                {
                    yield return Fabrication(relative, assignment, propertyName);
                }
            }
        }
    }

    private static string LastSegment(string dotted) => dotted[(dotted.LastIndexOf('.') + 1)..];

    private static bool IsFabricatedId(string propertyName, string rightHandSide) =>
        propertyName.EndsWith("Id", StringComparison.Ordinal)
        && FabricatedIdProperties.Contains(propertyName, StringComparer.OrdinalIgnoreCase)
        && rightHandSide.Contains("Guid.NewGuid()", StringComparison.Ordinal);

    private Violation Fabrication(string relative, SyntaxNode node, string propertyName) =>
        new(
            relative,
            node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            Id,
            $"'{propertyName} = Guid.NewGuid()' in a routed method reports an identifier that was never persisted.");

    private static MethodDeclarationSyntax? AnnotatedRouteMethod(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null)
        {
            return null;
        }

        return method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute => attribute.Name.ToString().Contains("Http", StringComparison.Ordinal)
                || attribute.Name.ToString().Contains("Route", StringComparison.Ordinal))
            ? method
            : null;
    }
}
