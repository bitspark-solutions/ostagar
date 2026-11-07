using Ostagar.Ledger;
using Ostagar.Modules.CashBook;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ostagar.Data;

/// <summary>
/// Maps the cash book's source documents. The invariants that make a posting correct live on the
/// ledger tables; what this table adds is the document side of the same discipline — a recorded
/// movement that always points at the entry it produced, so "the cash book said it and the ledger
/// did not" is a state the schema cannot express.
/// </summary>
public static class CashBookModelConfiguration
{
    public static void Apply(ModelBuilder builder)
    {
        var transaction = builder.Entity<CashTransaction>();

        TenantTableConfiguration.ApplyTenantScope(transaction, "cash_transactions");
        transaction.HasKey(x => x.Id);

        // Enums as text, per the ledger's convention: the legacy tree persisted enums as integers
        // while its model read strings, and the two silently described different tables.
        transaction.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16).IsRequired();
        transaction.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        transaction.Property(x => x.TransactionDate).HasColumnType("date").IsRequired();
        transaction.Property(x => x.Amount).HasColumnType("numeric(18,4)").IsRequired();
        transaction.Property(x => x.Currency).HasConversion(new CurrencyCodeConverter()).HasMaxLength(3).IsRequired();
        transaction.Property(x => x.Memo).HasMaxLength(500);

        transaction.ToTable("cash_transactions", table => table.HasCheckConstraint(
            "CK_cash_transactions_positive_amount", "amount > 0"));

        // The two accounts are distinct columns rather than a generic "account" pair, so the schema
        // itself can forbid the row that says money moved from a place to itself.
        transaction.ToTable("cash_transactions", table => table.HasCheckConstraint(
            "CK_cash_transactions_distinct_accounts", "cash_account_id <> counter_account_id"));

        transaction.HasOne<ChartOfAccount>()
            .WithMany()
            .HasForeignKey(x => x.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        transaction.HasOne<ChartOfAccount>()
            .WithMany()
            .HasForeignKey(x => x.CounterAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // The entry links are Restrict on both sides: the document may not outlive its entry's
        // existence claim, and the immutable entry may not be deleted to orphan the document.
        transaction.HasOne<GlEntry>()
            .WithMany()
            .HasForeignKey(x => x.PostedEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        transaction.HasOne<GlEntry>()
            .WithMany()
            .HasForeignKey(x => x.ReversedByEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The daily book is read by date, newest first; the index shape matches that scan.
        transaction.HasIndex(x => new { x.TenantId, x.TransactionDate });
        transaction.HasIndex(x => x.PostedEntryId);
    }
}
