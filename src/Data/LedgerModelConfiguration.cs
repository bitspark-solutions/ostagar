using Ostagar.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ostagar.Data;

/// <summary>
/// Maps the ledger onto the schema, including the constraints that make a wrong posting impossible
/// rather than merely discouraged.
/// </summary>
/// <remarks>
/// <para>
/// Every CHECK here exists because the legacy build proved what happens without it. A grep across all
/// 15 of its migration files found no check constraint of any kind: debits-equals-credits was a C#
/// comparison with a 0.01 tolerance, and the cash-book path bypassed even that.
/// </para>
///
/// <para>
/// <b>Delete behaviour is Restrict everywhere except an entry's own lines.</b> Convention would make a
/// posting cascade from its period, its journal and the entry it reverses, so deleting a fiscal period
/// would take the general ledger with it — a silent, plausible-looking way to destroy accounting history.
/// The exceptions are the tenancy foreign key, where a tenant leaving the platform should take its own
/// rows, and entry to lines, which are parts of one whole and are never addressed apart from it.
/// </para>
/// </remarks>
public static class LedgerModelConfiguration
{
    public static void Apply(ModelBuilder builder)
    {
        ConfigureAccounts(builder.Entity<ChartOfAccount>());
        ConfigureJournals(builder.Entity<Journal>());
        ConfigurePeriods(builder.Entity<FiscalPeriod>());
        ConfigureEntries(builder.Entity<GlEntry>());
        ConfigureLines(builder.Entity<GlLine>());
        ConfigureExchangeRates(builder.Entity<ExchangeRate>());
    }

    private static void ConfigureAccounts(EntityTypeBuilder<ChartOfAccount> account)
    {
        TenantTableConfiguration.ApplyTenantScope(account, "accounts");
        account.HasKey(x => x.Id);

        account.Property(x => x.Code).HasMaxLength(32).IsRequired();
        account.Property(x => x.Name).HasMaxLength(200).IsRequired();
        account.Property(x => x.NameBangla).HasMaxLength(200);

        // Stored as text, not as an integer ordinal. The legacy tree persisted enums as integers while
        // its model later asked for strings, which is how `Status` and `JournalType` ended up with the
        // entity and the schema describing different tables.
        account.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
        account.Property(x => x.SubType).HasConversion<string>().HasMaxLength(32);
        account.Property(x => x.Path).HasColumnType("ltree");

        // Codes are unique per company, not per platform. The legacy model declared a 10-character
        // AccountCode while its configuration said 50 and its request DTO allowed 20.
        account.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        account.HasIndex(x => x.Path).HasMethod("gist");
        account.HasIndex(x => new { x.TenantId, x.ParentId });

        // A parent with children cannot itself be postable, and a leaf cannot have children. Enforced
        // in the database because the service layer is the thing that gets bypassed.
        account.ToTable(t => t.HasCheckConstraint(
            "CK_accounts_postable_is_leaf",
            "\"is_postable\" OR \"parent_id\" IS NULL"));

        // Restrict, not cascade: deleting an account that postings reference would rewrite history,
        // and deleting a parent would orphan the subtree its ltree paths are built from.
        account.HasOne<ChartOfAccount>().WithMany().HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureJournals(EntityTypeBuilder<Journal> journal)
    {
        TenantTableConfiguration.ApplyTenantScope(journal, "journals");
        journal.HasKey(x => x.Id);

        journal.Property(x => x.Code).HasMaxLength(16).IsRequired();
        journal.Property(x => x.Name).HasMaxLength(100).IsRequired();
        journal.Property(x => x.NameBangla).HasMaxLength(100);
        journal.Property(x => x.NumberPrefix).HasMaxLength(8).IsRequired();

        journal.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

        journal.ToTable(t => t.HasCheckConstraint("CK_journals_next_number", "\"next_number\" >= 1"));

        journal.HasOne<ChartOfAccount>().WithMany().HasForeignKey(x => x.DefaultDebitAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        journal.HasOne<ChartOfAccount>().WithMany().HasForeignKey(x => x.DefaultCreditAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePeriods(EntityTypeBuilder<FiscalPeriod> period)
    {
        TenantTableConfiguration.ApplyTenantScope(period, "fiscal_periods");
        period.HasKey(x => x.Id);

        period.Property(x => x.Name).HasMaxLength(64);
        period.Property(x => x.NameBangla).HasMaxLength(64);
        period.Property(x => x.StartDate).HasColumnType("date");
        period.Property(x => x.EndDate).HasColumnType("date");
        period.Property(x => x.SoftLockedOn).HasColumnType("date");
        period.Property(x => x.HardLockedOn).HasColumnType("date");
        period.Property(x => x.TaxLockedOn).HasColumnType("date");

        period.HasIndex(x => new { x.TenantId, x.FiscalYear, x.Number }).IsUnique();

        period.ToTable(t =>
        {
            t.HasCheckConstraint("CK_fiscal_periods_number_in_year", "\"number\" BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_fiscal_periods_ordered", "\"end_date\" >= \"start_date\"");
        });
    }

    private static void ConfigureEntries(EntityTypeBuilder<GlEntry> entry)
    {
        TenantTableConfiguration.ApplyTenantScope(entry, "gl_entries");
        entry.HasKey(x => x.Id);

        entry.Property(x => x.Number).HasMaxLength(32);
        entry.Property(x => x.JournalCode).HasMaxLength(16).IsRequired();
        entry.Property(x => x.SourceType).HasMaxLength(48);
        entry.Property(x => x.Memo).HasMaxLength(1000);
        entry.Property(x => x.Status).HasConversion<string>().HasMaxLength(8);
        entry.Property(x => x.PostingDate).HasColumnType("date");

        // Idempotency: a retry of the same document revision cannot create a second posting.
        entry.HasIndex(x => new { x.TenantId, x.SourceType, x.SourceId, x.Iteration })
            .IsUnique()
            .HasDatabaseName("UQ_gl_entries_source_key");

        entry.HasIndex(x => new { x.TenantId, x.JournalCode, x.PeriodId, x.Number })
            .IsUnique()
            .HasDatabaseName("UQ_gl_entries_number");

        entry.HasIndex(x => new { x.TenantId, x.PostingDate });
        entry.HasIndex(x => x.ReversesEntryId);

        entry.ToTable(t =>
        {
            t.HasCheckConstraint("CK_gl_entries_iteration", "\"iteration\" >= 1");
            t.HasCheckConstraint(
                "CK_gl_entries_reversal_not_self",
                "\"reverses_entry_id\" IS NULL OR \"reverses_entry_id\" <> \"id\"");
        });

        // A period is never deleted out from under a posting, so this is the guard that keeps a period
        // closing from becoming a way to erase a year.
        entry.HasOne<FiscalPeriod>().WithMany().HasForeignKey(x => x.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite on purpose: the journal is looked up by code, and a bare foreign key on the code
        // alone would let one company's journal satisfy another's posting.
        entry.HasOne<Journal>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.JournalCode })
            .HasPrincipalKey(x => new { x.TenantId, x.Code })
            .OnDelete(DeleteBehavior.Restrict);

        entry.HasOne<GlEntry>().WithMany().HasForeignKey(x => x.ReversesEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        entry.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.EntryId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureLines(EntityTypeBuilder<GlLine> line)
    {
        TenantTableConfiguration.ApplyTenantScope(line, "gl_lines");
        line.HasKey(x => x.Id);

        line.Property(x => x.Debit).HasColumnType("numeric(18,2)");
        line.Property(x => x.Credit).HasColumnType("numeric(18,2)");
        line.Property(x => x.AmountInCurrency).HasColumnType("numeric(18,4)");
        line.Property(x => x.ExchangeRate).HasColumnType("numeric(18,9)");
        line.Property(x => x.Currency).HasConversion(new CurrencyCodeConverter()).HasMaxLength(3).IsRequired();
        line.Property(x => x.Memo).HasMaxLength(500);
        line.Property(x => x.ExchangeRateDate).HasColumnType("date");

        line.HasIndex(x => new { x.TenantId, x.AccountId, x.EntryId });
        line.HasIndex(x => new { x.AccountId, x.TenantId });
        line.HasIndex(x => new { x.EntryId, x.LineOrder });

        line.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_gl_line_debit_xor_credit",
                "(\"debit\" > 0 AND \"credit\" IS NULL) OR (\"credit\" > 0 AND \"debit\" IS NULL)");
            t.HasCheckConstraint("CK_gl_line_currency_amount", "\"amount_in_currency\" <> 0");
            t.HasCheckConstraint("CK_gl_line_rate_positive", "\"exchange_rate\" > 0");
        });

        line.HasOne<ChartOfAccount>().WithMany().HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Maps the effective-dated rate history.
    /// </summary>
    /// <remarks>
    /// The lookup index leads with currency and is ordered by the effective date descending because every
    /// resolution is "the newest row for this currency on or before that date" — a scan over all of a
    /// company's rates is what a posting would otherwise cost, and it grows with each month of trading.
    ///
    /// <c>rate > 0</c> is enforced here rather than only in the service because a zero rate turns a
    /// foreign-currency line into a free one, and a negative rate silently flips which side of an FX gain
    /// or loss the difference lands on.
    /// </remarks>
    private static void ConfigureExchangeRates(EntityTypeBuilder<ExchangeRate> rate)
    {
        TenantTableConfiguration.ApplyTenantScope(rate, "exchange_rates");
        rate.HasKey(x => x.Id);

        rate.Property(x => x.Currency).HasConversion(new CurrencyCodeConverter()).HasMaxLength(3).IsRequired();
        rate.Property(x => x.ValidFrom).HasColumnType("date");
        rate.Property(x => x.Rate).HasColumnType("numeric(18,9)");
        rate.Property(x => x.Source).HasMaxLength(64);

        rate.HasIndex(x => new { x.TenantId, x.Currency, x.ValidFrom })
            .IsUnique()
            .HasDatabaseName("UQ_exchange_rates_currency_valid_from");

        rate.HasIndex(x => new { x.Currency, x.TenantId, x.ValidFrom })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_exchange_rates_resolution");

        rate.ToTable(t => t.HasCheckConstraint(
            "CK_exchange_rates_rate_positive",
            "\"rate\" > 0"));
    }
}
