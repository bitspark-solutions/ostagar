namespace Ostagar.Api.Composition;

/// <summary>
/// Hardens the service provider against the exact failure that killed the legacy build: 22 of 35
/// service registrations were commented out, every controller still compiled, and each one threw
/// only when a request activated it. Here the container is asked to prove it can construct
/// everything it was handed, at startup, in every environment.
/// </summary>
internal static class StrictServiceProviderFactory
{
    public static IHostBuilder UseStrictServiceProvider(this IHostBuilder hostBuilder) =>
        hostBuilder.UseServiceProviderFactory(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));
}
