using FluentValidation;
using Granit.Validation.Internal;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for financial identifiers.
/// </summary>
public static class FinancialValidatorExtensions
{
    /// <summary>
    /// Validates a credit or debit card number per ISO/IEC 7812-1.
    /// </summary>
    /// <remarks>
    /// Accepts numbers with or without spaces and dashes (e.g. <c>4111 1111 1111 1111</c>).
    /// Verifies the Luhn check digit and validates the IIN prefix against known card networks
    /// (Visa, Mastercard, American Express, Discover, Diners Club, JCB, UnionPay, Maestro).
    /// <para>
    /// This method is named <c>CreditCardNumber</c> to avoid ambiguity with FluentValidation's
    /// built-in <c>CreditCard()</c> extension (which only validates Luhn without IIN prefix checks).
    /// </para>
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> CreditCardNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(CreditCardAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidCreditCard");

    /// <summary>
    /// Validates a Legal Entity Identifier (LEI) per ISO 17442.
    /// </summary>
    /// <remarks>
    /// A LEI is a 20-character alphanumeric code: 4-digit LOU prefix + 14-character entity identifier
    /// + 2 check digits validated using ISO 7064 MOD 97-10.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Lei<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(LeiAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidLei");
}
