using Ostagar.Core;

namespace Ostagar.Platform.Audit;

/// <summary>
/// What the application is asking to be recorded.
/// </summary>
/// <remarks>
/// The actor is part of the record rather than read from a thread-local inside the writer, so a CLI run
/// or a background job states whose name it is signing for instead of inheriting whatever identity
/// happened to be ambient. The legacy <c>GetCurrentUserId()</c> returned a hard-coded guid and every
/// audit row looked like it had been written by the same nobody.
/// </remarks>
public sealed record AuditRecord(
    Guid? ActorUserId,
    string ActorName,
    string Action,
    string Resource,
    string ResourceId,
    AuditOutcome Outcome,
    object? Detail = null,
    string? CorrelationId = null,
    string? SourceAddress = null);
