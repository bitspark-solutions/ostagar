using Ostagar.Ledger;

namespace Ostagar.Modules.CashBook;

/// <summary>
/// What <see cref="CashPosting.For"/> decided: a request ready to hand to <see cref="IPoster"/>,
/// or the reasons no request could be built.
/// </summary>
public sealed record CashPostingPlan
{
    public PostingRequest? Request { get; init; }

    public IReadOnlyList<CashTransactionError> Errors { get; init; } = [];

    public bool Succeeded => Request is not null;

    public static CashPostingPlan Ready(PostingRequest request) => new() { Request = request };

    public static CashPostingPlan Refused(params CashTransactionError[] errors) =>
        new() { Errors = errors };
}
