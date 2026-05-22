using FluentValidation;
using Granit.Validation.Europe.Internal.Spain;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for Spanish identifiers and address fields.
/// </summary>
public static class SpanishIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a Spanish NIF/DNI (Número de Identificación Fiscal / Documento Nacional de Identidad).
    /// </summary>
    /// <remarks>
    /// Expects 8 digits followed by 1 control letter. The control letter is computed from
    /// <c>number mod 23</c>. Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SpanishNif<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(NifAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidSpanishNif");

    /// <summary>
    /// Validates a Spanish NIE (Número de Identidad de Extranjero).
    /// </summary>
    /// <remarks>
    /// Format: X, Y, or Z + 7 digits + 1 control letter. The initial letter is replaced with
    /// a digit (X→0, Y→1, Z→2) and the NIF control algorithm is applied.
    /// Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SpanishNie<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(NieAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidSpanishNie");

    /// <summary>
    /// Validates a Spanish CIF (Código de Identificación Fiscal) for legal entities.
    /// </summary>
    /// <remarks>
    /// Format: 1 letter (organisation type) + 7 digits + 1 control character (digit or letter).
    /// Valid organisation types: A, B, C, D, E, F, G, H, J, K, L, M, N, P, Q, R, S, U, V, W.
    /// Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SpanishCif<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(CifAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidSpanishCif");

    /// <summary>
    /// Validates a Spanish postal code (código postal).
    /// </summary>
    /// <remarks>
    /// Expects 5 digits representing provinces 01–52.
    /// The first two digits identify the province, followed by 3 digits for the delivery zone.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SpanishPostalCode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SpanishPostalCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidSpanishPostalCode");
}
