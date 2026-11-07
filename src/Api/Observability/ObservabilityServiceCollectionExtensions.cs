using Serilog;

namespace Ostagar.Api.Observability;

/// <summary>
/// Composition-root wiring for request logging. Correlation is mandatory: the legacy audit trail
/// could not be joined to application logs because the controller and the audit service generated
/// different request identifiers.
/// </summary>
internal static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddGarmentsObservability(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddLogging(logging => logging.ClearProviders().AddSerilog(dispose: true));
        return services;
    }
}
