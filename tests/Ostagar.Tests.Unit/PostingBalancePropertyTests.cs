using FsCheck;
using FsCheck.Xunit;
using Ostagar.Core;
using Ostagar.Ledger;

namespace Ostagar.Tests.Unit;

/// <summary>
/// Proves the balancer's guarantees hold for postings nobody thought to write a test for.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PostingBalanceTests"/> pins the specific shapes the legacy build produced. This is the
/// general claim underneath them: there is no input at all for which the checker says yes and the ledger
/// is unbalanced. That distinction matters because a hand-written suite proves the cases someone
/// imagined, and the reason a ledger can still end up wrong is a combination no one was thinking about —
/// a split payment with an odd number of lines, a refund whose only credit is the last of nine.
/// </para>
///
/// <para>
/// Amounts come from byte-sized pennies rather than arbitrary decimals on purpose. <see cref="Money"/>
/// refuses a value whose scale exceeds the currency's, which is the intended behaviour, and letting the
/// generator produce 0.0000001 would test that refusal instead of this one.
/// </para>
/// </remarks>
public class PostingBalancePropertyTests
{
    private static readonly Guid Cash = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Income = Guid.Parse("22222222-0000-0000-0000-000000000002");

    /// <summary>
    /// The exact condition the balancer accepts on, stated both ways.
    /// </summary>
    /// <remarks>
    /// Written as an equivalence rather than "if it passes then the totals agree" because the generated
    /// postings here are already well-formed — one side per line, non-zero, one currency — so two lines
    /// and equal totals is not merely sufficient, it is the whole rule. The one-way version would be
    /// satisfied vacuously by a checker that never accepted anything, and the second half of this
    /// assertion is what makes that impossible.
    /// </remarks>
    [Property(MaxTest = 400)]
    public Property A_check_accepts_exactly_when_two_or_more_lines_balance(byte[] debitPennies, byte[] creditPennies)
    {
        var lines = Build(debitPennies, creditPennies);

        var totalDebit = debitPennies.Where(penny => penny > 0).Sum(penny => penny / 100m);
        var totalCredit = creditPennies.Where(penny => penny > 0).Sum(penny => penny / 100m);

        var result = PostingBalance.Check(lines);

        return (result.Succeeded == (lines.Count >= 2 && totalDebit == totalCredit)).ToProperty();
    }

    [Property(MaxTest = 400)]
    public Property A_check_that_passes_never_accepts_a_line_with_both_sides_or_no_side(
        byte[] debitPennies,
        byte[] creditPennies)
    {
        var lines = Build(debitPennies, creditPennies);

        var result = PostingBalance.Check(lines);

        return (!result.Succeeded
                || lines.All(line => (line.Debit is null) != (line.Credit is null))).ToProperty();
    }

    /// <summary>
    /// The other direction, and the one that keeps the properties above honest: a balancer that refused
    /// everything would make every claim above true of an empty set of accepted postings.
    /// </summary>
    [Property(MaxTest = 400)]
    public Property Any_matching_pair_of_sides_is_always_accepted(byte[] pennies)
    {
        var amounts = pennies.Where(penny => penny > 0).ToArray();

        // Nothing to balance is not a finding about the balancer.
        if (amounts.Length == 0)
        {
            return true.ToProperty();
        }

        var lines = new List<PostingLine>();

        foreach (var penny in amounts)
        {
            lines.Add(Debit(Cash, penny / 100m));
            lines.Add(Credit(Income, penny / 100m));
        }

        var result = PostingBalance.Check(lines);

        return (result.Succeeded
                && result.TotalDebit == result.TotalCredit
                && result.TotalDebit == amounts.Sum(penny => penny / 100m))
            .ToProperty();
    }

    /// <summary>
    /// Removing any single line from a balanced posting must break it, for postings of more than two
    /// lines. A balancer that tolerated a dropped line would balance the same entry twice.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Dropping_one_line_from_a_balanced_posting_always_refuses_it(byte[] pennies)
    {
        var amounts = pennies.Where(penny => penny > 0).Take(8).ToArray();

        if (amounts.Length < 2)
        {
            return true.ToProperty();
        }

        var lines = new List<PostingLine>();

        foreach (var penny in amounts)
        {
            lines.Add(Debit(Cash, penny / 100m));
            lines.Add(Credit(Income, penny / 100m));
        }

        Assert.True(PostingBalance.Check(lines).Succeeded);

        for (var dropped = 0; dropped < lines.Count; dropped++)
        {
            var withoutOne = lines.Where((_, index) => index != dropped).ToList();

            if (!PostingBalance.Check(withoutOne).Succeeded)
            {
                continue;
            }

            return false.ToProperty();
        }

        return true.ToProperty();
    }

    private static List<PostingLine> Build(byte[] debitPennies, byte[] creditPennies)
    {
        var lines = new List<PostingLine>();

        // Zero-valued pennies would become a zero amount, which the checker refuses as its own error and
        // which is not what these properties are about.
        foreach (var penny in debitPennies.Where(penny => penny > 0))
        {
            lines.Add(Debit(Cash, penny / 100m));
        }

        foreach (var penny in creditPennies.Where(penny => penny > 0))
        {
            lines.Add(Credit(Income, penny / 100m));
        }

        return lines;
    }

    private static PostingLine Debit(Guid accountId, decimal amount) => new()
    {
        AccountId = accountId,
        Debit = Money.Bdt(amount),
        AmountInCurrency = amount,
    };

    private static PostingLine Credit(Guid accountId, decimal amount) => new()
    {
        AccountId = accountId,
        Credit = Money.Bdt(amount),
        AmountInCurrency = amount,
    };
}
