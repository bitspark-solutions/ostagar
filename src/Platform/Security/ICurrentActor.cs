namespace Ostagar.Platform.Security;

/// <summary>
/// Reads the authenticated caller for the current operation.
/// </summary>
/// <remarks>
/// No default actor, and nothing that fabricates one. A path with no caller asks for one explicitly the
/// way the command line does, rather than inheriting a placeholder that later reads as a real person in
/// the audit trail.
/// </remarks>
public interface ICurrentActor
{
    bool IsAuthenticated { get; }

    /// <summary>Null when the request is anonymous or its subject claim cannot be read.</summary>
    ActorIdentity? Current { get; }

    string? CorrelationId { get; }

    string? SourceAddress { get; }
}
