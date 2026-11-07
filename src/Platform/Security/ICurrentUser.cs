using Ostagar.Core;

namespace Ostagar.Platform.Security;

/// <summary>
/// Who the current operation is being done for.
/// </summary>
/// <remarks>
/// The actor id is read from the subject claim of the access token and nowhere else. The legacy
/// <c>GetCurrentUserId()</c> returned a hard-coded guid behind a <c>// TODO</c>, so every audit row
/// claimed the same fictional person wrote it and the trail could not be used for anything.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The tenant this request was resolved into, or an empty value when there is none.</summary>
    TenantId TenantId { get; }

    /// <summary>The subject claim, parsed. Null when the request is anonymous or the claim is malformed.</summary>
    Guid? UserId { get; }

    string UserName { get; }

    string DisplayName { get; }
}
