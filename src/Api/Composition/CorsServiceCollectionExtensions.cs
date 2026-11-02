using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Garments.Api.Composition;

/// <summary>
/// Cross-origin access, configured rather than assumed.
/// </summary>
/// <remarks>
/// The web app lives on a different origin than the API even in development, so a browser request
/// must be answered by policy rather than by accident. No configured origins means no cross-origin
/// access at all — the same deny-by-default stance as authorization, where exposure is a decision
/// someone wrote down. Bearer tokens ride in the <c>Authorization</c> header, so credentials mode is
/// not needed and a wildcard origin would be a hole, not a convenience.
/// </remarks>
internal static class CorsServiceCollectionExtensions
{
    internal const string SectionName = "Cors";

    internal static IServiceCollection AddGarmentsCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration
            .GetSection($"{SectionName}:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (origins.Length == 0)
        {
            return services;
        }

        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(origins)
            .WithHeaders("Content-Type", "Authorization")
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")));

        return services;
    }
}
