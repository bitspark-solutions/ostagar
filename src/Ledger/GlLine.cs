using Ostagar.Core;

namespace Ostagar.Ledger;

/// <summary>
/// One side of one posting. The row where the accounting invariants actually live.
/// </summary>
/// <remarks>
/// <para><b>Debit and credit are separate nullable columns, not a single signed amount.</b> A signed
/// column is tempting and loses information: "0" and "no entry on this side" become the same thing, and
/// the balance check becomes arithmetic on signs instead of two sums that must be equal. The database
/// enforces exactly-one-of with a CHECK, which a signed column cannot express.</para>
///
/// <para><b>Both currencies are stored on the line.</b> <see cref="AmountInCurrency"/> is what the
/// invoice said; <see cref="Debit"/>/<see cref="Credit"/> are the functional-currency figures the ledger
/// balances. Storing only the converted amount makes a foreign-currency original impossible to
/// reconcile, which is the difference between an FX revaluation entry and a guess. This is the
/// Odoo <c>debit</c>/<c>amount_currency</c> and ERPNext
/// <c>debit_in_account_currency</c> pattern.</para>
///
/// <para><b>The rate is snapshotted with its effective date.</b> Re-reading a rate table at report time
/// would rewrite history every time a rate is corrected.</para>
/// </remarks>
public class GlLine : ITenantScoped
{
    public Guid Id { get; set; }

    public TenantId TenantId { get; set; }

    public Guid EntryId { get; set; }

    public Guid AccountId { get; set; }

    /// <summary>Stable display order within the entry, so a printed journal is reproducible.</summary>
    public int LineOrder { get; set; }

    /// <summary>Functional-currency debit. Null when this line does not debit.</summary>
    public decimal? Debit { get; set; }

    /// <summary>Functional-currency credit. Null when this line does not credit.</summary>
    public decimal? Credit { get; set; }

    public CurrencyCode Currency { get; set; }

    /// <summary>The amount in the transaction currency, signed positive on whichever side it falls.</summary>
    public decimal AmountInCurrency { get; set; }

    /// <summary>Transaction → functional rate applied, kept even when they are the same currency.</summary>
    public decimal ExchangeRate { get; set; } = 1m;

    /// <summary>Which day's rate <see cref="ExchangeRate"/> is. Without it the number is unexplainable.</summary>
    public DateOnly ExchangeRateDate { get; set; }

    /// <summary>Optional analysis dimension, e.g. a department or cost centre for dimensional P&amp;L.</summary>
    public Guid? AnalysisId { get; set; }

    public string Memo { get; set; } = string.Empty;

    /// <summary>
    /// The account-determination rule version that produced this line's account, or null when a human
    /// chose it. Snapshotting it is what lets a rule change without rewriting history.
    /// </summary>
    public Guid? RuleVersionId { get; set; }
}
