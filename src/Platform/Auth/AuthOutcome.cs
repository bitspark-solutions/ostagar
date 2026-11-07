using Ostagar.Platform.Identity;

namespace Ostagar.Platform.Auth;

/// <summary>
/// The result of a sign-in attempt.
/// </summary>
/// <remarks>
/// <see cref="Reason"/> is an enum rather than a message string so the client can translate it. The
/// legacy build returned <c>ex.Message</c> straight to the browser, which leaked internal exception
/// text through a public endpoint.
///
/// Unknown tenant, unknown account and wrong password all collapse to one reason on purpose: anything
/// noisier turns the sign-in form into a way to enumerate which companies and which people exist.
/// </remarks>
/// <param name="Succeeded">True only when a token was issued.</param>
/// <param name="Reason">Why the attempt failed, when it did.</param>
/// <param name="Token">The issued access token.</param>
/// <param name="Roles">System roles granted to the account.</param>
public sealed record AuthOutcome(
    bool Succeeded,
    AuthFailureReason Reason,
    IssuedAccessToken? Token = null,
    IReadOnlyList<string>? Roles = null)
{
    public static AuthOutcome Rejected(AuthFailureReason reason) => new(false, reason);
}
