namespace Ostagar.Api.Health;

/// <summary>
/// Liveness and readiness probes. Deliberately the only anonymous endpoints besides auth, so a
/// scanner finding an unauthenticated surface has something to compare against.
/// </summary>
internal static class HealthServiceCollectionExtensions
{
    public static IServiceCollection AddGarmentsHealth(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }
}
