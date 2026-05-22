using FluentValidation;
using Granit.Validation.Europe.Internal;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for personal identifiers issued to natural persons.
/// </summary>
public static class PersonalIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a Belgian National Identification Number (NISS / INSZ / SSIN).
    /// </summary>
    /// <remarks>
    /// Accepts both formatted (<c>85.07.30-033.28</c>) and unformatted (<c>85073003328</c>) forms.
    /// Validates the 97-modulo check digit for persons born before and from 2000.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BelgianNiss<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(NissAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidBelgianNiss");

    /// <summary>
    /// Validates a French NIR (Numéro d'Identification au Répertoire / Numéro de Sécurité Sociale).
    /// </summary>
    /// <remarks>
    /// Expects 15 characters: sex + 2-digit year + 2-digit month + 5-digit INSEE commune code
    /// + 3-digit order + 2-digit check key. Departments 2A and 2B (Corse) are supported.
    /// Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchNir<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(NirAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidFrenchNir");

    /// <summary>
    /// Validates a Belgian electronic identity card number (eID).
    /// </summary>
    /// <remarks>
    /// Accepts 12 digits, optionally formatted as <c>NNN-NNNNNNN-NN</c>.
    /// The last two digits are the check pair: <c>97 − (first 10 digits mod 97)</c>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BelgianEid<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(EidAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidBelgianEid");
}
