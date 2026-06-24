using FluentValidation;
using Granit.Validation.Europe.Internal.Luxembourg;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for Luxembourg identifiers.
/// </summary>
public static class LuxembourgishIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a Luxembourg national identification number (matricule national).
    /// </summary>
    /// <remarks>
    /// Expects 13 digits (YYYYMMDDXXXCC) with a valid Luhn check.
    /// Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> LuxembourgMatricule<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(LuxembourgMatriculeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:LuxembourgMatricule");

    /// <summary>
    /// Validates a Luxembourg RCS number (Registre de Commerce et des Sociétés).
    /// </summary>
    /// <remarks>
    /// Expects a letter prefix (A–J or S) followed by 1 to 6 digits.
    /// Spaces are stripped and input is uppercased before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> LuxembourgRcs<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(LuxembourgRcsAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:LuxembourgRcs");
}
