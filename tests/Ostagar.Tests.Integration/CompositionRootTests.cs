using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Boots the real composition root and proves every controller in the product can be constructed
/// by it.
/// </summary>
/// <remarks>
/// The legacy build compiled cleanly while 22 of its 35 service registrations sat commented out in
/// Program.cs; each affected controller threw only when a request reached it, and no request ever
/// reached them because the frontend could not build either. This test makes that state
/// unshippable: a controller whose dependencies were never registered fails the gate, not the
/// customer. Controllers are discovered from the assemblies rather than from the route table, so a
/// controller that was never mapped cannot slip through by being unreachable.
/// </remarks>
public class CompositionRootTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    [Fact]
    public void Every_controller_can_be_constructed_by_the_composition_root()
    {
        var controllerTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("Ostagar.", StringComparison.Ordinal) == true)
            .SelectMany(assembly => SafeTypes(assembly))
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();

        var failures = new List<string>();

        foreach (var controllerType in controllerTypes)
        {
            using var scope = factory.Services.CreateScope();

            try
            {
                _ = ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);
            }
            catch (InvalidOperationException exception)
            {
                failures.Add($"{controllerType.Name}: {exception.Message}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count} controller(s) cannot be constructed — a dependency is missing from the composition root:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task Health_probes_respond_without_credentials()
    {
        var client = factory.CreateClient();

        Assert.True((await client.GetAsync("/health/live")).IsSuccessStatusCode);
        Assert.True((await client.GetAsync("/health/ready")).IsSuccessStatusCode);
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
