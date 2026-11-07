namespace Ostagar.Modules.Partners;

/// <summary>
/// The partner's code: a sequence value issued by the numbering service, never a string derived from the
/// name and never random.
/// </summary>
/// <remarks>
/// <para>
/// This is the rule the legacy project broke and the one that must not be re-broken. It auto-created
/// chart-of-accounts rows from a typed-in category name with a code from <c>new Random().Next(100, 999)</c>.
/// A random code collides (roughly once every nine hundred rows, so a tenant with a thousand parties hits a
/// collision and either gets a duplicate key error or, worse, an <c>ignore</c>), it cannot be read back in
/// order, and it means the code carries no information about what it names. A code derived from the name is
/// the opposite failure: renaming the party orphans the code, and two parties with similar names collide by
/// construction — <c>RAHIMA</c> and <c>RAHIMA TRADERS</c> are the same clerk's keystrokes.
/// </para>
///
/// <para>
/// So the value is only ever produced by <see cref="IPartnerCodeAllocator"/>, and this type refuses
/// anything that does not look like an issued sequence: no spaces, no lowercase, bounded length, and at
/// least one digit. The digit requirement is what makes "SONALI COTTON" impossible to store as a code even
/// when someone pastes it in; the aggregate adds a second guard
/// (<see cref="Partner"/>) that refuses a code whose letters are simply the name's letters.
/// </para>
/// </remarks>
public readonly record struct PartnerCode
{
    private const int MinLength = 4;
    private const int MaxLength = 20;

    private readonly string _value;

    private PartnerCode(string value) => _value = value;

    public string Value => _value ?? string.Empty;

    public bool IsEmpty => _value is null;

    /// <summary>The letters of the code, uppercased and separated from the sequence part — for the code-vs-name guard.</summary>
    public string Letters => PartnerTextRules.LettersOnly(Value);

    /// <summary>
    /// Parses a code that has already been allocated. Validates the shape of an issued code; it does not
    /// invent one, and there is no constructor path that accepts free text.
    /// </summary>
    /// <exception cref="ArgumentException">The text is not shaped like an allocated code.</exception>
    public static PartnerCode From(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var candidate = raw.Trim().ToUpperInvariant();

        if (candidate.Length is < MinLength or > MaxLength)
        {
            throw new ArgumentException(
                $"'{raw}' is not a partner code: an allocated code is between {MinLength} and {MaxLength} characters.",
                nameof(raw));
        }

        if (candidate.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                $"'{raw}' contains a space. A code names one row; a phrase does not.",
                nameof(raw));
        }

        var hasDigit = false;

        foreach (var character in candidate)
        {
            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                hasDigit |= char.IsDigit(character);
                continue;
            }

            if (character is not ('-' or '.' or '/' or '_'))
            {
                throw new ArgumentException(
                    $"'{raw}' contains '{character}', which an allocated code never does.",
                    nameof(raw));
            }
        }

        if (!hasDigit)
        {
            throw new ArgumentException(
                $"'{raw}' has no sequence digits, so it is a word rather than an allocated code. "
                + "Codes are never derived from a name.",
                nameof(raw));
        }

        return new PartnerCode(candidate);
    }

    public static bool TryFrom(string? raw, out PartnerCode result)
    {
        try
        {
            result = From(raw ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            result = default;
            return false;
        }
    }

    public override string ToString() => Value;
}
