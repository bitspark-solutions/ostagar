using Ostagar.Core;

namespace Ostagar.Modules.CashBook;

public sealed class BankAccount : ITenantScoped
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public CurrencyCode Currency { get; set; } = CurrencyCode.Bdt;
}
