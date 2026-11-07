using Ostagar.Data;
using Ostagar.Platform.Rbac;
using Ostagar.Platform.Security;
using Ostagar.Platform.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ostagar.Cli;

/// <summary>
/// Builds the service container the offline commands run against.
/// </summary>
/// <remarks>
/// Deliberately the same registrations the API host makes, so a command cannot succeed against a
/// wiring the application never uses. It reads the same configuration keys and therefore the same
/// environment, which is what lets one deployment boundary govern both.
/// </remarks>
internal static class CliHost
{
    public static IHostBuilder Build(string[] args) =>
        Host.CreateDefaultBuilder(args)
            // An operator reads this command's output. EF's parameterised SQL at Information level
            // buries the one line that matters and puts account data in terminal scrollback.
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .ConfigureAppConfiguration((_, configuration) => configuration.AddEnvironmentVariables())
            .ConfigureServices((context, services) =>
            {
                var databaseOptions = DataServiceCollectionExtensions.ReadDatabaseOptions(context.Configuration);

                services.AddGarmentsData(databaseOptions);
                services.AddIdentityCore<Platform.Identity.ApplicationUser>()
                    .AddRoles<Platform.Identity.ApplicationRole>()
                    .AddEntityFrameworkStores<OstagarDbContext>();
                services.AddHttpContextAccessor();
                services.AddScoped<ICurrentUser, CurrentUserService>();
                services.AddScoped<ICurrentActor, CurrentActorService>();
                services.AddScoped<PermissionSeeder>();
            });
}
