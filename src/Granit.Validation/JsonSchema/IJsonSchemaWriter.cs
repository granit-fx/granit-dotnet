using System.Text.Json.Nodes;

namespace Granit.Validation.JsonSchema;

/// <summary>
/// Writes a JSON Schema fragment describing the validation constraints declared
/// on a type via FluentValidation.
/// </summary>
/// <remarks>
/// <para>
/// Maps FluentValidation rules to JSON Schema Draft 7 keywords:
/// <c>maxLength</c>, <c>minLength</c>, <c>pattern</c>, <c>minimum</c>, <c>maximum</c>,
/// <c>exclusiveMinimum</c>, <c>exclusiveMaximum</c>, <c>format</c>, and the parent
/// <c>required</c> array.
/// </para>
/// <para>
/// Custom Granit validators (IBAN, E.164, etc.) are exposed as
/// <c>x-granit-validator</c> string properties carrying the structured error code
/// (e.g. <c>Granit:Validation:InvalidIban</c>). Pattern hint validators emit a
/// companion <c>x-granit-pattern-hint</c> property alongside <c>pattern</c>.
/// </para>
/// <para>
/// Async validators (<c>MustAsync</c>) and complex custom validators without
/// declarative constraints are silently ignored — they have no JSON Schema equivalent.
/// </para>
/// </remarks>
public interface IJsonSchemaWriter
{
    /// <summary>
    /// Writes a JSON Schema fragment for the validation constraints declared on the
    /// given type. Returns <see langword="null"/> when no <see cref="FluentValidation.IValidator{T}"/>
    /// is registered for the type.
    /// </summary>
    /// <param name="type">The type whose validator should be inspected.</param>
    /// <returns>
    /// A JSON object shaped like a JSON Schema fragment with <c>properties</c> and the
    /// optional <c>required</c> array, or <see langword="null"/> if no validator is registered.
    /// Property names use the JSON-friendly camelCase convention.
    /// </returns>
    JsonObject? Write(Type type);
}
