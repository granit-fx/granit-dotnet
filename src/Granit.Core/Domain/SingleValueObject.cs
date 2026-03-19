namespace Granit.Core.Domain;

/// <summary>
/// Base class for value objects that wrap a single primitive value.
/// Provides automatic equality, <see cref="ToString"/>, and serves as the marker type
/// for <c>SingleValueObjectJsonConverterFactory</c> and EF Core conventions.
/// </summary>
/// <typeparam name="T">The underlying primitive type (e.g., <see cref="string"/>, <see cref="int"/>).</typeparam>
/// <remarks>
/// <para>
/// Subclasses should be <c>sealed</c>, expose <see cref="Value"/> as <c>init</c>,
/// and provide a static <c>Create</c> factory with validation.
/// </para>
/// <example>
/// <code>
/// public sealed class ContentType : SingleValueObject&lt;string&gt;
/// {
///     public override required string Value { get; init; }
///     public static ContentType Create(string value) { ... }
///     public static implicit operator string(ContentType ct) => ct.Value;
///     public static implicit operator ContentType(string s) => Create(s);
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class SingleValueObject<T> : ValueObject
    where T : notnull
{
    /// <summary>
    /// The underlying primitive value.
    /// </summary>
    public abstract T Value { get; init; }

    /// <inheritdoc />
    protected sealed override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public sealed override string ToString() => Value.ToString() ?? string.Empty;
}
