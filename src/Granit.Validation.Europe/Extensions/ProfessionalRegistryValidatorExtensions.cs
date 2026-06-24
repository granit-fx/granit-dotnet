using FluentValidation;
using Granit.Validation.Europe.Internal;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for professional registry identifiers
/// (healthcare professionals and regulated establishments).
/// </summary>
public static class ProfessionalRegistryValidatorExtensions
{
    /// <summary>
    /// Validates a French RPPS number (Répertoire Partagé des Professionnels de Santé).
    /// </summary>
    /// <remarks>
    /// Expects exactly 11 digits with a valid Luhn check digit.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchRpps<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(RppsAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:FrenchRpps");

    /// <summary>
    /// Validates a French ADELI number (Automatisation DEs LIstes).
    /// </summary>
    /// <remarks>
    /// Expects exactly 9 digits.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchAdeli<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(AdeliAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:FrenchAdeli");

    /// <summary>
    /// Validates a French Finess number (Fichier National des Établissements Sanitaires et Sociaux).
    /// </summary>
    /// <remarks>
    /// Expects 9 digits with a valid Luhn check digit.
    /// Identifies French health establishments (hospitals, clinics, pharmacies).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchFiness<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(FinesAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:FrenchFiness");

    /// <summary>
    /// Validates a Belgian INAMI number (Institut National d'Assurance Maladie-Invalidité / RIZIV).
    /// </summary>
    /// <remarks>
    /// Accepts 11 digits, optionally formatted as <c>XXXXXX/XXX-XX</c>.
    /// The last two digits are the check pair: <c>97 − (first 9 digits mod 97)</c>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BelgianInami<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(InamiAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:BelgianInami");
}
