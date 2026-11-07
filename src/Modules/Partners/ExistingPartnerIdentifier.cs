namespace Ostagar.Modules.Partners;

/// <summary>
/// One identifier already stored against another partner, as the duplicate check receives it.
/// </summary>
/// <remarks>
/// The narrowest shape that answers "does this identifier already belong to somebody" without dragging the
/// whole aggregate, or a persistence layer, into a pure rule. Whoever implements
/// <see cref="IPartnerDuplicateDirectory"/> projects into this; the evaluator never needs a name, an address
/// or a balance, and a check that could see those would eventually be tempted to decide on them.
/// </remarks>
/// <param name="PartnerId">The existing partner that already holds the value.</param>
/// <param name="Code">Its code, so a review screen can name the row a person has to open.</param>
/// <param name="LegalName">Its legal name, for the same reason.</param>
/// <param name="Identifier">The stored identifier that collides.</param>
public sealed record ExistingPartnerIdentifier(
    PartnerId PartnerId,
    PartnerCode Code,
    PartnerName LegalName,
    PartnerIdentifier Identifier);
