using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.Finance.Internal;

namespace Granit.Validation.Finance.Extensions;

/// <summary>
/// FluentValidation extension methods for international banking and payment identifiers: account and
/// creditor identifiers (IBAN, BIC/SWIFT, SEPA Creditor Identifier) and domestic routing/clearing
/// codes (US ABA, AU/NZ BSB, Canadian routing, Indian IFSC).
/// </summary>
public static class FinancePaymentValidatorExtensions
{
    /// <summary>
    /// Validates an International Bank Account Number (IBAN) per ISO 13616.
    /// </summary>
    /// <remarks>
    /// Accepts IBANs with or without spaces (e.g. <c>BE68 5390 0754 7034</c>).
    /// Validates the MOD-97 check digits. Supports all country formats (15–34 characters).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Iban<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(IbanAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:Iban");

    /// <summary>
    /// Validates a BIC/SWIFT code per ISO 9362.
    /// </summary>
    /// <remarks>
    /// Accepts 8-character (primary office) and 11-character (branch) codes. Case-insensitive.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BicSwift<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(BicSwiftAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:BicSwift");

    /// <summary>
    /// Validates a SEPA Creditor Identifier (SCI) per EPC262-08.
    /// </summary>
    /// <remarks>
    /// Format: 2-alpha country + 2-digit check + 3 alphanumeric creditor business code + national ID.
    /// Validated using ISO 7064 MOD 97-10 (same algorithm as IBAN).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SepaCreditorIdentifier<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SepaCreditorIdentifierAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:SepaCreditorIdentifier");

    /// <summary>
    /// Validates a US ABA routing transit number (9 digits with the ABA mod-10 checksum).
    /// </summary>
    public static IRuleBuilderOptions<T, string?> AbaRouting<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(AbaRoutingAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:AbaRouting");

    /// <summary>
    /// Validates an Australian/New Zealand BSB (Bank-State-Branch) code (6 digits, optional hyphen).
    /// </summary>
    public static IRuleBuilderOptions<T, string?> Bsb<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(BsbAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:Bsb");

    /// <summary>
    /// Validates a Canadian routing number (5-digit transit + 3-digit institution, or the 9-digit
    /// electronic MICR form).
    /// </summary>
    public static IRuleBuilderOptions<T, string?> CanadianRouting<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(CanadianRoutingAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:CanadianRouting");

    /// <summary>
    /// Validates an Indian Financial System Code (IFSC) — 11 characters per RBI format.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> Ifsc<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(IfscAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:Ifsc");
}
