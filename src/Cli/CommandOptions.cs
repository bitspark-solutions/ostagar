namespace Ostagar.Cli;

/// <summary>
/// Parsed and validated command-line arguments.
/// </summary>
/// <remarks>
/// The password is accepted as an option only because a provisioning step needs it; it is documented
/// as preferring <c>--password-env</c>, since a password in argv is visible in the process list and in
/// shell history on a shared build box.
/// </remarks>
internal sealed class CommandOptions
{
    internal const string TenantFlag = "--tenant";
    internal const string EmailFlag = "--email";
    internal const string PasswordFlag = "--password";
    internal const string PasswordEnvFlag = "--password-env";

    internal List<string> Problems { get; } = [];

    internal string? TenantSlug { get; private set; }

    internal string? Email { get; private set; }

    internal string? Password { get; private set; }

    internal static CommandOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CommandOptions();

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case TenantFlag when index + 1 < args.Count:
                    options.TenantSlug = args[++index];
                    break;
                case EmailFlag when index + 1 < args.Count:
                    options.Email = args[++index];
                    break;
                case PasswordFlag when index + 1 < args.Count:
                    options.Password = args[++index];
                    break;
                case PasswordEnvFlag when index + 1 < args.Count:
                    options.Password = Environment.GetEnvironmentVariable(args[++index]);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.TenantSlug))
        {
            options.Problems.Add($"{TenantFlag} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Email))
        {
            options.Problems.Add($"{EmailFlag} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            options.Problems.Add(
                $"A password is required via {PasswordFlag} or {PasswordEnvFlag} naming an environment variable.");
        }

        return options;
    }
}
