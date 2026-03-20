using System.Text.RegularExpressions;
using FluentValidation;
using Granit.Validation.Europe.Internal;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for company and legal entity identifiers.
/// </summary>
public static partial class CompanyIdentifierValidatorExtensions
{
    // NAF/APE code: 4 digits + 1 uppercase letter (e.g. 6201Z).
    [GeneratedRegex(@"^\d{4}[A-Z]$", RegexOptions.None, 100)]
    private static partial Regex NafCodeRegex();

    /// <summary>
    /// Validates a French SIREN number (Système d'Identification du Répertoire des ENtreprises).
    /// </summary>
    /// <remarks>
    /// Expects exactly 9 digits with a valid Luhn check digit.
    /// Spaces are stripped before validation (<c>732 829 320</c> is accepted).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchSiren<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SirenAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Granit:Validation:InvalidFrenchSiren");

    /// <summary>
    /// Validates a French SIRET number (Système d'Identification du Répertoire des ÉTablissements).
    /// </summary>
    /// <remarks>
    /// Expects exactly 14 digits (SIREN 9 + NIC 5) with a valid Luhn check digit over all 14 digits.
    /// Spaces are stripped before validation (<c>732 829 320 00074</c> is accepted).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchSiret<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SiretAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Granit:Validation:InvalidFrenchSiret");

    /// <summary>
    /// Validates a Belgian BCE enterprise number (Banque-Carrefour des Entreprises / KBO).
    /// </summary>
    /// <remarks>
    /// Accepts 10 digits, optionally formatted with dots (<c>0xxx.xxx.xxx</c>) or spaces.
    /// The last two digits are the check pair: <c>97 − (first 8 digits mod 97)</c>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BelgianBce<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(BceAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Granit:Validation:InvalidBelgianBce");

    /// <summary>
    /// Validates a French NAF/APE activity code (Nomenclature des Activités Françaises).
    /// </summary>
    /// <remarks>
    /// Format: 4 digits followed by 1 uppercase letter (e.g. <c>6201Z</c> for software development).
    /// Case-insensitive — input is normalised to uppercase before matching.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchNafCode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && NafCodeRegex().IsMatch(value.Trim().ToUpperInvariant()))
            .WithErrorCodeAndMessage("Granit:Validation:InvalidFrenchNafCode");

    // -------------------------------------------------------------------------
    // Server-side single-field validation delegates
    // -------------------------------------------------------------------------

    internal static bool IsValidFrenchNafCode(string? value) =>
        value is not null && NafCodeRegex().IsMatch(value.Trim().ToUpperInvariant());
}
