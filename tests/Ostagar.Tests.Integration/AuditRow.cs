using Ostagar.Platform.Audit;

namespace Ostagar.Tests.Integration;

/// <summary>
/// One audit row as the database holds it, for asserting what the trail recorded.
/// </summary>
/// <remarks>
/// Read back from the table rather than captured from a mock, because the claim under test is that a
/// posting and its record are written by the same commit. A double would prove the code called a
/// logger and nothing about whether anything survived.
/// </remarks>
internal sealed record AuditRow(
    long Id,
    string Action,
    string Resource,
    string ResourceId,
    AuditOutcome Outcome,
    Guid? ActorUserId,
    string ActorName,
    string Detail);
