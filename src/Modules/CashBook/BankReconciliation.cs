using Ostagar.Core;

namespace Ostagar.Modules.CashBook;

public sealed class BankReconciliation : ITenantScoped
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid BankAccountId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<BankReconciliationMatch> Matches { get; set; } = [];
}
