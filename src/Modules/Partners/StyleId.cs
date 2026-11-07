using System.Globalization;

namespace Ostagar.Modules.Partners;

/// <summary>
/// A reference to one style in the inventory catalogue, held as an id and nothing else.
/// </summary>
/// <remarks>
/// A price list is per style, and the tempting implementation is to copy the style's name, fabric and
/// size curve onto the price line so the screen has something to show. That is how a price list ends up
/// quoting a style whose name was corrected in the catalogue last week, and how two modules disagree about
/// the same object on the same invoice line. The catalogue is Inventory's aggregate; this module holds its
/// key and asks (<see cref="IStyleCatalog"/>) when it needs to know the row is real.
/// </remarks>
public readonly record struct StyleId
{
    private readonly Guid _value;

    private StyleId(Guid value) => _value = value;

    public Guid Value => _value;

    public bool IsEmpty => _value == Guid.Empty;

    public static StyleId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A style reference must name a style.", nameof(value))
            : new StyleId(value);

    public static bool TryParse(string? value, out StyleId result)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            result = new StyleId(parsed);
            return true;
        }

        result = default;
        return false;
    }

    public override string ToString() => _value.ToString("D", CultureInfo.InvariantCulture);
}
