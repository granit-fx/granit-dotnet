using FluentValidation;
using Granit.Validation.Internal;
using Granit.Validation.OpenApi;

namespace Granit.Validation.Extensions;

/// <summary>
/// Extension methods on <see cref="IRuleBuilderOptions{T,TProperty}"/> for Granit conventions.
/// </summary>
public static class RuleBuilderExtensions
{
    /// <summary>
    /// Sets <c>WithErrorCode</c> and <c>WithMessage</c> on the rule builder to the same value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The error <c>code</c> is preserved on <c>ValidationFailure.ErrorCode</c> (machine-readable),
    /// while the message is resolved at validation time to the localized template registered under
    /// the same key in the <c>Validation</c> resource — so the <c>errors</c> map carries a real
    /// sentence in the request culture, not the bare code. Any FluentValidation placeholders in the
    /// template (e.g. <c>{PropertyName}</c>) are interpolated by the message formatter.
    /// </para>
    /// <para>
    /// Resolution is lazy (per validation) and degrades to the bare <c>code</c> when the
    /// localization layer is not wired (e.g. unit tests instantiating a validator directly).
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <typeparam name="TProperty">The property type.</typeparam>
    /// <param name="rule">The rule builder options to configure.</param>
    /// <param name="code">The <c>Validation:*</c> error code (also used as message key).</param>
    /// <returns>The same rule builder options for fluent chaining.</returns>
    public static IRuleBuilderOptions<T, TProperty> WithErrorCodeAndMessage<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule, string code) =>
        rule.WithErrorCode(code)
            .WithMessage(_ => GranitErrorCodeLanguageManager.ResolveMessage(code));

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
    /// The frontend resolves the key via <c>GET /api/{version}/localization</c>.
    /// </para>
    /// <example>
    /// <code>
    /// RuleFor(x => x.CountryCode)
    ///     .Matches(@"^[A-Z]{2}$")
    ///     .WithPatternHint("Validation:Hints:Alpha2Code");
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
