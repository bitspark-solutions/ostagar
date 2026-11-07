namespace Ostagar.Modules.Partners;

/// <summary>
/// Thrown when a partner rule refuses an operation. Carries a <see cref="PartnerErrorCode"/> so the refusal
/// can be translated, logged and asserted on without parsing a sentence.
/// </summary>
/// <remarks>
/// Deliberately a refusal, not a failure: nothing here is a bug, and nothing here is a result object that
/// reports success while doing nothing. The legacy tree's habit of swallowing a validation problem and
/// returning a fabricated identifier is the exact defect this exception type makes impossible — an operation
/// that cannot be honoured stops here, before any caller can be told it worked.
///
/// Guards on the aggregate (<see cref="Partner.RequirePaymentChannel"/>, <see cref="Partner.Deactivate"/>)
/// throw this. Rules the caller wants to inspect instead of catch — duplicate detection and the credit check
/// — return a value, because those are questions a screen has to answer before a person commits.
/// </remarks>
public sealed class PartnerRuleException : Exception
{
    public PartnerRuleException(PartnerErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public PartnerRuleException(PartnerErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Standard exception constructor, for plumbing and tests. A rule refusal always names its code.</summary>
    public PartnerRuleException(string message)
        : base(message)
    {
        Code = PartnerErrorCode.Unspecified;
    }

    public PartnerRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = PartnerErrorCode.Unspecified;
    }

    public PartnerRuleException()
    {
        Code = PartnerErrorCode.Unspecified;
    }

    /// <summary>Which rule refused. The message is for logs; the UI translates this.</summary>
    public PartnerErrorCode Code { get; }
}
