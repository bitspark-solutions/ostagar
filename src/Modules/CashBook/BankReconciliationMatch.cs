using Ostagar.Core;

namespace Ostagar.Modules.CashBook;

public sealed class BankReconciliationMatch : ITenantScoped
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid ReconciliationId { get; set; }
    public Guid GlLineId { get; set; }
}
