using FluentValidation;
using FluentValidation.Validators;

namespace Granit.Validation.OpenApi;

/// <summary>
/// No-op validator that carries a pattern hint i18n key as metadata for the
/// <see cref="FluentValidationSchemaTransformer"/>.
/// </summary>
/// <remarks>
/// This validator always returns <see langword="true"/>. It exists solely to transport
/// the <see cref="IPatternHintProvider.HintKey"/> from the validator definition to the
/// OpenAPI schema transformer, which emits it as <c>x-granit-pattern-hint</c>.
/// </remarks>
/// <typeparam name="T">The type being validated.</typeparam>
/// <typeparam name="TProperty">The property type.</typeparam>
public sealed class PatternHintValidator<T, TProperty>(string hintKey)
    : PropertyValidator<T, TProperty>, IPatternHintProvider
{
    /// <inheritdoc />
    public string HintKey { get; } = hintKey
        ?? throw new ArgumentNullException(nameof(hintKey));

    /// <inheritdoc />
    public override string Name => "PatternHintValidator";

    /// <inheritdoc />
    public override bool IsValid(ValidationContext<T> context, TProperty value) => true;
}
