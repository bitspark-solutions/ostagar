using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace Ostagar.Api.Health;

/// <summary>Maps the health surface onto the endpoint pipeline.</summary>
internal static class HealthEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapGarmentsHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => true })
            .AllowAnonymous();

        return endpoints;
    }
}
