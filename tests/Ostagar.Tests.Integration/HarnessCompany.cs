using System.Net.Http;

namespace Ostagar.Tests.Integration;

/// <summary>
/// A provisioned company with a signed-in caller attached to it.
/// </summary>
/// <remarks>
/// Carries its own HTTP client because <c>WebApplicationFactory</c> gives each client its own default
/// request headers, and a test that needs two clerks in one company must not have them share one set of
/// authorization headers.
/// </remarks>
/// <param name="World">The company's ledger fixture.</param>
/// <param name="Token">A token issued to this company by the real login endpoint.</param>
/// <param name="Client">A client that sends that token.</param>
internal sealed record HarnessCompany(LedgerWorld World, string Token, HttpClient Client);
