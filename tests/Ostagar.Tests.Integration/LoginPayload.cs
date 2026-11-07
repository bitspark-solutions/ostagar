namespace Ostagar.Tests.Integration;

/// <summary>
/// The body of a successful login, for tests that need a real token rather than a constructed one.
/// </summary>
/// <remarks>
/// Signed in through the endpoint instead of minting a token in the test, because the chain under test
/// includes the token the product actually issues: a hand-built token can prove the middleware while a
/// real one proves the middleware, the claim names and the mapping between them.
/// </remarks>
internal sealed record LoginPayload(string Token);
