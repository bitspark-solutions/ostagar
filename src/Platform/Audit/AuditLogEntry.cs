using Ostagar.Core;
using Ostagar.Platform.Tenancy;

namespace Ostagar.Platform.Audit;

/// <summary>
/// One append-only line in the tenant's audit trail.
/// </summary>
/// <remarks>
/// The legacy <c>AuditLogs</c> table was created by no migration at all, and its service caught every
/// exception. Auditing was therefore invisible in two directions: the table did not exist in a fresh
/// database, and in a live one a failed write was indistinguishable from an event that never happened.
/// This type is created by the first migration, its table is <c>REVOKE</c>d for UPDATE and DELETE, and
/// <see cref="IAuditWriter"/> lets failures propagate.
///
/// Actor identity comes from the access token, never from the row being touched, so a clerk editing
/// someone else's record is recorded as themselves.
/// </remarks>
public class AuditLogEntry : ITenantScoped
{
    public long Id { get; set; }

    public TenantId TenantId { get; set; }

    /// <summary>
    /// The authenticated user behind the action. Null only for an anonymous attempt that was refused;
    /// it is never filled with a placeholder identity.
    /// </summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Copy of the display name at the time of the event, so the trail still reads after the
    /// user is renamed or deleted.</summary>
    public string ActorName { get; set; } = string.Empty;

    public required string Action { get; set; }

    public required string Resource { get; set; }

    public string ResourceId { get; set; } = string.Empty;

    public AuditOutcome Outcome { get; set; }

    public string Detail { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string SourceAddress { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}
