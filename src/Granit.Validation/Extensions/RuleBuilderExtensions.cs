using FluentValidation;
using Granit.Validation.OpenApi;

namespace Granit.Validation.Extensions;

/// <summary>
/// Extension methods on <see cref="IRuleBuilderOptions{T,TProperty}"/> for Granit conventions.
/// </summary>
public static class RuleBuilderExtensions
{
    /// <summary>
    /// Sets <see cref="IRuleBuilderOptions{T,TProperty}.WithErrorCode"/> and
    /// <see cref="IRuleBuilderOptions{T,TProperty}.WithMessage"/> to the same value.
    /// </summary>
    /// <remarks>
    /// In Granit validators the error code is also the message key so that the Wolverine HTTP
    /// middleware serializes it in <c>ValidationProblemDetails.errors</c>.
    /// Using this method prevents the two values from silently diverging.
    /// </remarks>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="rule">The rule builder options to configure.</param>
    /// <param name="code">The <c>Granit:Validation:*</c> error code (also used as message key).</param>
    /// <returns>The same rule builder options for fluent chaining.</returns>
    public static IRuleBuilderOptions<T, TProperty> WithErrorCodeAndMessage<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule, string code) =>
        rule.WithErrorCode(code).WithMessage(code);

    /// <summary>
    /// Attaches a pattern hint i18n key to the current rule chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chain after <c>.Matches(regex)</c> to provide the frontend with a human-readable hint
    /// for the pattern (e.g. "2 uppercase letters" instead of <c>^[A-Z]{2}$</c>).
    /// </para>
    /// <para>
    /// The <see cref="FluentValidationSchemaTransformer"/> emits the hint key as the
    /// <c>x-granit-pattern-hint</c> OpenAPI extension alongside the <c>pattern</c> property.
    /// The frontend resolves the key via <c>GET /api/granit/localization</c>.
    /// </para>
    /// <example>
    /// <code>
    /// RuleFor(x => x.CountryCode)
    ///     .Matches(@"^[A-Z]{2}$")
    ///     .WithPatternHint("Granit:Validation:Hints:Alpha2Code");
    /// </code>
    /// </example>
    /// </remarks>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="rule">The rule builder options to configure.</param>
    /// <param name="hintKey">The i18n key for the pattern hint.</param>
    /// <returns>The same rule builder options for fluent chaining.</returns>
    public static IRuleBuilderOptions<T, TProperty> WithPatternHint<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule, string hintKey) =>
        rule.SetValidator(new PatternHintValidator<T, TProperty>(hintKey));
}
