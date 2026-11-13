namespace Ostagar.Api.Controllers;

public sealed record CreateBankAccountRequest(Guid LedgerAccountId, string Code, string BankName,
    string AccountNumber, string Currency);
