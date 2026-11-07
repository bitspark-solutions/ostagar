using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ostagar.Core;
using Ostagar.Platform.Auth;
using Ostagar.Platform.Security;
using Ostagar.Data;
using Ostagar.Platform.Identity;
using Ostagar.Platform.Rbac;
using Ostagar.Platform.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Ostagar.Tests.Integration;

/// <summary>
/// Proves two seeded tenants cannot see each other through the endpoints.
/// </summary>
/// <remarks>
/// This is the P1 gate. It goes through HTTP with real tokens rather than calling services directly,
/// because the property under test is the whole chain: token carries a tenant, middleware resolves it,
/// the session sets it, row-level security applies it. Every one of those links can be individually
/// correct while the chain leaks, and the legacy build is the proof — it had a permission attribute,
/// a permission service and permission checks, and none of them were reachable because the seeding
/// call was commented out.
/// </remarks>
public sealed class TenantIsolationTests(WebAppFactory factory) : IClassFixture<WebAppFactory>
{
    private const string SharedPassword = "Isolation-suite-strong-password-42";

    [Fact]
    public async Task A_tenant_cannot_read_another_tenants_departments()
    {
        var acme = await ProvisionAsync("acme-" + ShortId());
        var other = await ProvisionAsync("other-" + ShortId());

        // A row planted directly in the other tenant, so the assertion cannot be satisfied by an
        // empty database.
        await SeedDepartmentAsync(other.TenantId, "OTH-1", "Stitching Line 3");

        var acmeToken = await SignInAsync(acme);

        var response = await ReadAsAsync(acmeToken, "/api/v1/departments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var visible = await response.Content.ReadFromJsonAsync<DepartmentDto[]>();

        Assert.NotNull(visible);
        Assert.DoesNotContain(visible, department => department.Code == "OTH-1");
    }

    [Fact]
    public async Task A_tenant_cannot_write_into_another_tenants_department_list()
    {
        var acme = await ProvisionAsync("acme-" + ShortId());
        var other = await ProvisionAsync("other-" + ShortId());

        var acmeToken = await SignInAsync(acme);

        var created = await PostAsAsync(acmeToken, "/api/v1/departments",
            new { Code = "ACME-1", Name = "Cutting", NameBangla = "কাটিং", SortOrder = 1 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // The written row must land in the caller's own tenant and nowhere else.
        var inOther = await ReadAsAsync(await SignInAsync(other), "/api/v1/departments");
        var otherDepartments = await inOther.Content.ReadFromJsonAsync<DepartmentDto[]>();

        Assert.NotNull(otherDepartments);
        Assert.DoesNotContain(otherDepartments, department => department.Code == "ACME-1");
    }

    [Fact]
    public async Task An_anonymous_request_is_refused_rather_than_returning_everything()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/departments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_token_that_names_no_tenant_cannot_reach_the_database()
    {
        var acme = await ProvisionAsync("acme-" + ShortId());

        // Signed with the real key and genuinely missing the tenant claim. The earlier attempt here
        // string-replaced "tid" inside the base64url payload, which produced a token for a tenant
        // that does not exist rather than one with no tenant — and that correctly returns an empty
        // list, so the test was asserting the wrong thing while appearing to pass.
        var withoutTenant = IssueTokenWithoutTenantClaim();

        var response = await ReadAsAsync(withoutTenant, "/api/v1/departments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<TenantFixture> ProvisionAsync(string slug)
    {
        using var systemScope = TenantScope.OpenSystem(factory.Services.GetRequiredService<IServiceScopeFactory>());

        var directory = systemScope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenantId = TenantId.New();

        var tenant = await directory.CreateAsync(new Tenant
        {
            Id = tenantId,
            Slug = slug,
            DisplayName = slug,
        });

        using var tenantScope = TenantScope.Open(factory.Services.GetRequiredService<IServiceScopeFactory>(), tenant.Id);

        await tenantScope.ServiceProvider.GetRequiredService<PermissionSeeder>()
            .SeedTenantRolesAsync(tenant.Id);

        var users = tenantScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var email = $"owner@{slug}.test";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserName = email,
            Email = email,
            FullName = "Owner",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var created = await users.CreateAsync(user, SharedPassword);
        Assert.True(created.Succeeded, string.Join("; ", created.Errors));

        await users.AddToRoleAsync(user, DefaultRoleMatrix.Owner);

        return new TenantFixture(tenant.Id, user.Id, slug);
    }

    private async Task SeedDepartmentAsync(TenantId tenantId, string code, string name)
    {
        using var scope = TenantScope.Open(factory.Services.GetRequiredService<IServiceScopeFactory>(), tenantId);
        var context = scope.ServiceProvider.GetRequiredService<OstagarDbContext>();

        context.Departments.Add(new Department
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = name,
            SortOrder = 1,
            IsActive = true,
        });

        await context.SaveChangesAsync();
    }

    private static string IssueTokenWithoutTenantClaim()
    {
        var now = DateTime.UtcNow;
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(WebAppFactory.TestSigningKey));

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "ostagar-test",
            audience: "ostagar-web-test",
            claims: [new System.Security.Claims.Claim(AuthClaimNames.Subject, Guid.NewGuid().ToString("D"))],
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(
                key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> SignInAsync(TenantFixture fixture)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            TenantSlug = fixture.Slug,
            Email = $"owner@{fixture.Slug}.test",
            Password = SharedPassword,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        return payload.Token;
    }

    private async Task<HttpResponseMessage> ReadAsAsync(string token, string path)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(path);
    }

    private async Task<HttpResponseMessage> PostAsAsync(string token, string path, object body)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsJsonAsync(path, body);
    }

    private static string ShortId() => Guid.NewGuid().ToString("N")[..8];

    private sealed record TenantFixture(TenantId TenantId, Guid UserId, string Slug);

    private sealed record DepartmentDto(Guid Id, string Code, string Name, string NameBangla, bool IsActive);

    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, string[] Roles);
}
