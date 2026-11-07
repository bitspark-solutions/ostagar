using System.Text.Json;
using Ostagar.Core;
using Ostagar.Platform.Audit;
using Ostagar.Platform.Tenancy;

namespace Ostagar.Data;

/// <summary>
/// Stages audit entries into the unit of work that is already running.
/// </summary>
/// <remarks>
/// <para>
/// It does not save, and that is the whole design. An audit row describes a change; if the two commit
/// separately then one of them can be a lie. Saved immediately, a posting that rolls back afterwards
/// leaves a permanent record of money that never moved; saved by the caller afterwards, a change can
/// land with nothing describing who made it. Tracking into the same context means the entry and its
/// audit row are written by the same commit or by neither.
/// </para>
///
/// <para>
/// The consequence to know: <b>appending without a later save loses the row.</b> Every caller therefore
/// owns a unit of work. That is a real hazard rather than a neatness, and it is why the legacy build's
/// writer — which caught and swallowed every exception — produced an audit trail that looked populated
/// while being silently incomplete, which is worse than an empty one because nobody investigates an
/// empty trail.
/// </para>
///
/// <para>
/// It does not swallow failures either. If the audit row cannot be written, the change that depends on
/// it does not happen; a ledger whose trail can be quietly omitted is not an auditable ledger.
/// </para>
/// </remarks>
public sealed class DatabaseAuditWriter(OstagarDbContext context, ITenantContext tenantContext) : IAuditWriter
{
    public void Append(AuditRecord record)
    {
        var tenantId = tenantContext.TenantId;

        if (tenantId.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Refusing to append an audit entry for '{record.Action}' without a resolved tenant. "
                + "An unattributed audit row is not a lesser record — it is a row no company can ever "
                + "be shown or defended with.");
        }

        context.AuditLogs.Add(new AuditLogEntry
        {
            TenantId = tenantId,
            ActorUserId = record.ActorUserId,
            ActorName = record.ActorName,
            Action = record.Action,
            Resource = record.Resource,
            ResourceId = record.ResourceId,
            Outcome = record.Outcome,
            // jsonb, so never an empty string. An absent payload is "nothing further recorded", which
            // is a value; an empty string would fail the column and take the change down with it.
            Detail = record.Detail is null
                ? "{}"
                : JsonSerializer.Serialize(record.Detail),
            CorrelationId = record.CorrelationId ?? string.Empty,
            SourceAddress = record.SourceAddress ?? string.Empty,
            OccurredAt = DateTimeOffset.UtcNow,
        });
    }
}
