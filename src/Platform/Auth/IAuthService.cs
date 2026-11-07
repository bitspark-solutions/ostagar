namespace Ostagar.Platform.Auth;

/// <summary>Turns submitted credentials into an access token.</summary>
public interface IAuthService
{
    Task<AuthOutcome> SignInAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
