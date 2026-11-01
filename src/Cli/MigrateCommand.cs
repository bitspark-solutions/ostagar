using Garments.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Garments.Cli;

/// <summary>
/// Applies pending migrations, offline, as the migration principal.
/// </summary>
/// <remarks>
/// This is the container deploy step: the compose topology runs it once to completion before the API
/// starts, which is why the API itself carries <c>MigrateOnStartup = false</c> — a schema change is a
/// deploy act, not a side effect of whichever web replica came up first.
/// </remarks>
internal static class MigrateCommand
{
    internal static async Task<int> RunAsync(string[] args)
    {
        using var host = CliHost.Build(args).Build();

        var runner = host.Services.GetRequiredService<MigrationRunner>();
        var applied = await runner.ApplyPendingAsync().ConfigureAwait(false);

        if (applied.Count == 0)
        {
            Console.WriteLine("No pending migrations.");
        }
        else
        {
            foreach (var migration in applied)
            {
                Console.WriteLine($"Applied {migration}");
            }
        }

        return 0;
    }
}
