namespace Ostagar.Platform.Audit;

/// <summary>
/// Puts entries into the tenant's audit trail.
/// </summary>
/// <remarks>
/// There is no save here on purpose. An audit entry is tracked into the same unit of work as the change
/// it describes, so one commit writes both or neither. The alternative — a writer that saves by itself —
/// is how an audit trail ends up recording a failure for money that was actually moved, or staying
/// silent while a row changed. The legacy build's writer swallowed every exception, which made the
/// second case indistinguishable from the first.
/// </remarks>
public interface IAuditWriter
{
    void Append(AuditRecord record);
}
