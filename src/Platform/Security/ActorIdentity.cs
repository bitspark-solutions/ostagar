namespace Ostagar.Platform.Security;

/// <summary>
/// The person an action is attributed to.
/// </summary>
/// <remarks>
/// A value, not a service: an audit row and a token issue need the same pair, and passing it around keeps
/// <see cref="Platform.Audit"/> independent of whether the caller came from a request or from the
/// command line.
/// </remarks>
public sealed record ActorIdentity(Guid UserId, string Name);
