using Garments.Api.Auth;
using Garments.Api.Composition;
using Garments.Api.Health;
using Garments.Api.Middleware;
using Garments.Api.Observability;
using Garments.Data;
using Garments.Platform.Rbac;
using Garments.Platform.Tenancy;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Fails at startup, not at request time, on anything the container cannot actually build.
builder.Host.UseStrictServiceProvider();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GarmentsERP"));

// Controllers before authorization, so the pipeline order cannot drift: an authorization service
// without a controller pipeline hides nothing, it just has nothing to protect.
builder.Services.AddControllers();

builder.Services.AddGarmentsObservability();
builder.Services.AddGarmentsHealth();

// Refuses to start without a configured connection rather than coming up and failing on the first
// query, which is how the legacy tree's "dependency check middleware" managed to report healthy
// against a database it had not actually migrated.
builder.Services.AddGarmentsData(DataServiceCollectionExtensions.ReadDatabaseOptions(builder.Configuration));

// Deny by default. An endpoint that is public is public because it is written down, not because
// nobody remembered to lock it. The legacy build shipped a bare AddAuthorization() and an
// unauthenticated POST /api/AdminSetup/create-admin with it.
builder.Services.AddGarmentsAuth(AuthServiceCollectionExtensions.ReadAccessTokenOptions(builder.Configuration));

// Cross-origin access is a configured list, not a default: an empty Cors:AllowedOrigins means the
// browser can never reach this API except same-origin, which is the correct reading of "private"
// until someone names a web origin.
builder.Services.AddGarmentsCors(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();

// Production only: redirecting under a test host turns every probe into a 307 and hides the
// response the test is asserting on. /health is excluded because a load balancer or container
// orchestrator probing over plain HTTP must get a 200, not a redirect it will not follow.
if (app.Environment.IsProduction())
{
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/health"),
        branch => branch.UseHttpsRedirection());
}

app.UseCors();

app.UseAuthentication();

// Between authentication and authorization: the tenant comes from the token, and nothing downstream
// may reach the database before it is resolved.
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapGarmentsHealth();

// The permission catalogue is seeded on every start, and the seeder is idempotent. Leaving this call
// commented out is what made the legacy [RequirePermission] attribute decorative: the checks were
// real, and the table they read was empty.
using (var scope = TenantScope.OpenSystem(app.Services.GetRequiredService<IServiceScopeFactory>()))
{
    scope.ServiceProvider.GetRequiredService<PermissionSeeder>()
        .SeedCatalogueAsync(CancellationToken.None).GetAwaiter().GetResult();
}

app.Run();

/// <summary>
/// Exposed so the test host boots the identical composition root the deployment uses: a DI
/// container that only works under the test host is the failure mode that hid for a year.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public partial class Program;
