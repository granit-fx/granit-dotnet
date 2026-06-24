using FluentValidation;
using Granit.Validation.Europe.Internal.Italy;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for Italian identifiers and address codes.
/// </summary>
public static class ItalianIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates an Italian Codice Fiscale (fiscal code for natural persons).
    /// </summary>
    /// <remarks>
    /// Expects 16 alphanumeric characters: surname consonants + first name consonants +
    /// birth date encoding + municipality code + check character.
    /// Spaces are stripped and input is uppercased before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> ItalianCodiceFiscale<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(CodiceFiscaleAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:ItalianCodiceFiscale");

    /// <summary>
    /// Validates an Italian Partita IVA (VAT identification number).
    /// </summary>
    /// <remarks>
    /// Expects exactly 11 digits with a valid Luhn-variant check digit.
    /// Spaces are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> ItalianPartitaIva<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(PartitaIvaAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:ItalianPartitaIva");

    /// <summary>
    /// Validates an Italian postal code (CAP — Codice di Avviamento Postale).
    /// </summary>
    /// <remarks>
    /// Expects exactly 5 digits with the first two digits in the range 00–98.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> ItalianPostalCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(ItalianPostalCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:ItalianPostalCode");
}
