using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Boots the shipped <c>Program</c> rather than a parallel test startup, so what the gate verifies
/// is what deploys. A test host wired differently from production is how the legacy tree convinced
/// itself a subsystem worked.
/// </summary>
/// <remarks>
/// The connection string is supplied here because the application refuses to start without one, and
/// that refusal is the behaviour under test, not an obstacle. It points at the local dev container;
/// tests that actually read or write must not rely on this one — they bring up their own database,
/// because a tenancy test run against an empty or absent schema proves nothing about row-level
/// security.
/// </remarks>
public class WebAppFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// A throwaway signing key for the test host. It is not a secret: it signs tokens that can only
    /// reach a test database, and it exists so the application's refusal to start without a key can
    /// stay absolute instead of being relaxed for tests.
    /// </summary>
    internal const string TestSigningKey = "integration-test-only-key-do-not-use-in-any-real-deployment-0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // No fallback connection string. A default pointing at a database that cannot exist turns a
        // missing environment into a confusing authentication failure, or worse, a test suite that
        // quietly runs against whatever happens to be listening. The gate exports this.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__Default must be set to run the integration tests. Run them through "
                + "./verify-all.sh, which starts the dev stack and exports it.");

        builder.UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:Default", connectionString)
            .UseSetting("OstagarAuth:Issuer", "ostagar-test")
            .UseSetting("OstagarAuth:Audience", "ostagar-web-test")
            .UseSetting("OstagarAuth:SigningKey", TestSigningKey);
    }
}
