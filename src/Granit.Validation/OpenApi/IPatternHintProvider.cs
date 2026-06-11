namespace Granit.Validation.OpenApi;

/// <summary>
/// Marker interface for validators that carry a pattern hint i18n key.
/// </summary>
/// <remarks>
/// Detected by <see cref="FluentValidationSchemaTransformer"/> to emit the
/// <c>x-granit-pattern-hint</c> OpenAPI extension alongside a <c>pattern</c> property.
/// The frontend uses this key to display a human-readable hint (e.g. "2 uppercase letters")
/// instead of showing the raw regex to the user.
/// </remarks>
public interface IPatternHintProvider
{
    /// <summary>
    /// The i18n key for the pattern hint (e.g. <c>Validation:Hint:Alpha2Code</c>).
    /// </summary>
    string HintKey { get; }
}
