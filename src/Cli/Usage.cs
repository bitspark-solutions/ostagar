namespace Ostagar.Cli;

/// <summary>The text printed when the command was invoked without a usable shape.</summary>
internal static class Usage
{
    internal const int ExitCode = 64;

    internal static void Print() =>
        Console.Error.WriteLine(
            """
            ostagar-cli <command> [options]

            Commands:
              superuser create   Create the first Owner account for a tenant, offline.
              db migrate         Apply pending migrations. The compose topology runs this before the API starts.

            Options for superuser create:
              --tenant <slug>          Required. The company's url slug.
              --email <address>        Required.
              --password <value>       Accepted, but visible in the process list.
              --password-env <NAME>    Preferred: read the password from this environment variable.

            The connection string is read from ConnectionStrings__Default, the same as the API host.
            There is no HTTP route that creates administrators; this command is the only path.
            """);
}
