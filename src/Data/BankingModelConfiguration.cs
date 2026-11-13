using Microsoft.EntityFrameworkCore;
using Ostagar.Ledger;
using Ostagar.Modules.CashBook;

namespace Ostagar.Data;

public static class BankingModelConfiguration
{
    public static void Apply(ModelBuilder builder)
    {
        var bank = builder.Entity<BankAccount>();
        TenantTableConfiguration.ApplyTenantScope(bank, "bank_accounts");
        bank.HasKey(x => x.Id);
        bank.Property(x => x.Code).HasMaxLength(BankingVocabulary.CodeLength);
        bank.Property(x => x.BankName).HasMaxLength(BankingVocabulary.BankNameLength);
        bank.Property(x => x.AccountNumber).HasMaxLength(BankingVocabulary.AccountNumberLength);
        bank.Property(x => x.Currency).HasConversion(new CurrencyCodeConverter()).HasMaxLength(BankingVocabulary.CurrencyLength);
        bank.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        bank.HasIndex(x => new { x.TenantId, x.LedgerAccountId }).IsUnique();
        bank.HasIndex(x => new { x.TenantId, x.BankName, x.AccountNumber }).IsUnique();
        bank.HasOne<ChartOfAccount>().WithMany().HasForeignKey(x => x.LedgerAccountId).OnDelete(DeleteBehavior.Restrict);
        bank.ToTable("bank_accounts", t => t.HasCheckConstraint("CK_bank_accounts_identity",
            "btrim(code) <> '' AND btrim(bank_name) <> '' AND btrim(account_number) <> '' AND currency ~ '^[A-Z]{3}$'"));

        var statement = builder.Entity<BankReconciliation>();
        TenantTableConfiguration.ApplyTenantScope(statement, "bank_reconciliations");
        statement.HasKey(x => x.Id);
        statement.Property(x => x.OpeningBalance).HasColumnType(BankingVocabulary.AmountType);
        statement.Property(x => x.ClosingBalance).HasColumnType(BankingVocabulary.AmountType);
        statement.Property(x => x.From).HasColumnName("from_date");
        statement.Property(x => x.To).HasColumnName("to_date");
        statement.HasIndex(x => new { x.TenantId, x.BankAccountId, x.To }).IsUnique();
        statement.HasOne<BankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        statement.HasMany(x => x.Matches).WithOne().HasForeignKey(x => x.ReconciliationId).OnDelete(DeleteBehavior.Restrict);
        statement.ToTable("bank_reconciliations", t => t.HasCheckConstraint("CK_bank_reconciliations_dates", "from_date <= to_date"));

        var match = builder.Entity<BankReconciliationMatch>();
        TenantTableConfiguration.ApplyTenantScope(match, "bank_reconciliation_matches");
        match.HasKey(x => x.Id);
        match.HasIndex(x => new { x.TenantId, x.GlLineId }).IsUnique();
        match.HasOne<GlLine>().WithMany().HasForeignKey(x => x.GlLineId).OnDelete(DeleteBehavior.Restrict);
    }
}
