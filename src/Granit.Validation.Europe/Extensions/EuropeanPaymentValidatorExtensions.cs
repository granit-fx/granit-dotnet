using FluentValidation;
using Granit.Validation.Europe.Internal;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for France/Belgium-specific payment identifiers.
/// </summary>
public static class EuropeanPaymentValidatorExtensions
{
    /// <summary>
    /// Validates a French RIB (Relevé d'Identité Bancaire) bank account number.
    /// </summary>
    /// <remarks>
    /// A RIB is 23 characters: 5-digit bank code + 5-digit branch code + 11-character account number
    /// + 2-digit check key. The account number may contain letters A–Z.
    /// Key formula: <c>clé = 97 − (89 × banque + 15 × guichet + 3 × compte) mod 97</c> (or 97 when 0).
    /// Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchRib<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(FrenchRibAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidFrenchRib");

    /// <summary>
    /// Validates a Belgian bank account number in the legacy pre-IBAN format.
    /// </summary>
    /// <remarks>
    /// Format: <c>NNN-NNNNNNN-NN</c> (3-digit bank code + 7-digit account + 2-digit check pair).
    /// The check pair equals <c>(first 10 digits) mod 97</c>, or 97 when the remainder is zero.
    /// Dashes and spaces are stripped before validation.
    /// <para>
    /// This format pre-dates IBAN. Belgian banks display IBAN on all statements since 2014.
    /// </para>
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BelgianAccountNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(BelgianAccountNumberAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidBelgianAccountNumber");
}
