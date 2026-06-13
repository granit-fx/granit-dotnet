using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Granit.Validation.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Validation.JsonSchema;

/// <summary>
/// Default <see cref="IJsonSchemaWriter"/> implementation. Resolves the registered
/// <see cref="IValidator{T}"/> for the requested type and projects its rules onto a
/// JSON Schema Draft 7 <see cref="JsonObject"/>.
/// </summary>
internal sealed class JsonSchemaWriter(IServiceScopeFactory scopeFactory) : IJsonSchemaWriter
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorTypeCache = new();

    /// <inheritdoc />
    public JsonObject? Write(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        Type validatorType = ValidatorTypeCache.GetOrAdd(
            type,
            static t => typeof(IValidator<>).MakeGenericType(t));

        using IServiceScope scope = scopeFactory.CreateScope();

        if (scope.ServiceProvider.GetService(validatorType) is not IValidator validator)
        {
            return null;
        }

        IValidatorDescriptor descriptor = validator.CreateDescriptor();

        JsonObject schema = [];
        JsonObject properties = [];
        JsonArray? required = null;

        foreach (System.Reflection.PropertyInfo prop in type.GetProperties())
        {
            string camelName = ToCamelCase(prop.Name);
            JsonObject propertySchema = [];
            string? patternHint = null;

            foreach ((IPropertyValidator propertyValidator, IRuleComponent component)
                in descriptor.GetValidatorsForMember(prop.Name))
            {
                if (propertyValidator is IPatternHintProvider hintProvider)
                {
                    patternHint = hintProvider.HintKey;
                    continue;
                }

                ApplyConstraint(
                    propertySchema,
                    ref required,
                    propertyValidator,
                    component,
                    camelName);
            }

            if (patternHint is not null && propertySchema.ContainsKey("pattern"))
            {
                propertySchema["x-granit-pattern-hint"] = patternHint;
            }

            if (propertySchema.Count > 0)
            {
                properties[camelName] = propertySchema;
            }
        }

        if (properties.Count > 0)
        {
            schema["properties"] = properties;
        }

        if (required is { Count: > 0 })
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static void ApplyConstraint(
        JsonObject propertySchema,
        ref JsonArray? required,
        IPropertyValidator validator,
        IRuleComponent component,
        string propertyName)
    {
        switch (validator)
        {
            // A conditional NotNull/NotEmpty (.When/.WhenAsync) — or a per-element rule from
            // RuleForEach — is not an unconditional requirement. OpenAPI has no conditional-required,
            // so promoting it to `required` over-constrains the contract (clients would be forced to
            // send the property even when the guard is false). The server still enforces it at runtime.
            case INotNullValidator or INotEmptyValidator
                when !component.HasCondition && !component.HasAsyncCondition:
                required ??= [];
                if (!required.Any(node => node?.GetValue<string>() == propertyName))
                {
                    required.Add(propertyName);
                }

                break;

            case IMaximumLengthValidator maxLength:
                propertySchema["maxLength"] = maxLength.Max;
                break;

            case IMinimumLengthValidator minLength:
                propertySchema["minLength"] = minLength.Min;
                break;

            case ILengthValidator length:
                propertySchema["minLength"] = length.Min;
                propertySchema["maxLength"] = length.Max;
                break;

            case IBetweenValidator between:
                if (TryGetNumericNode(between.From) is { } fromNode)
                {
                    propertySchema["minimum"] = fromNode;
                }

                if (TryGetNumericNode(between.To) is { } toNode)
                {
                    propertySchema["maximum"] = toNode;
                }

                break;

            case IComparisonValidator comparison:
                ApplyComparisonConstraint(propertySchema, comparison);
                break;

            case IRegularExpressionValidator regex:
                propertySchema["pattern"] = regex.Expression;
                break;

            case IEmailValidator:
                propertySchema["format"] = "email";
                break;

            default:
                string? errorCode = component.ErrorCode;
                if (errorCode?.StartsWith("Validation:", StringComparison.Ordinal) == true)
                {
                    propertySchema["x-granit-validator"] = errorCode;
                }

                break;
        }
    }

    private static void ApplyComparisonConstraint(
        JsonObject propertySchema,
        IComparisonValidator comparison)
    {
        if (TryGetNumericNode(comparison.ValueToCompare) is not { } node)
        {
            return;
        }

        switch (comparison.Comparison)
        {
            case Comparison.GreaterThanOrEqual:
                propertySchema["minimum"] = node;
                break;
            case Comparison.GreaterThan:
                propertySchema["exclusiveMinimum"] = node;
                break;
            case Comparison.LessThanOrEqual:
                propertySchema["maximum"] = node;
                break;
            case Comparison.LessThan:
                propertySchema["exclusiveMaximum"] = node;
                break;
        }
    }

    private static JsonValue? TryGetNumericNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            int i => JsonValue.Create(i),
            long l => JsonValue.Create(l),
            short s => JsonValue.Create(s),
            byte b => JsonValue.Create(b),
            decimal m => JsonValue.Create(m),
            double d => JsonValue.Create(d),
            float f => JsonValue.Create(f),
            IConvertible convertible => JsonValue.Create(
                convertible.ToString(CultureInfo.InvariantCulture)),
            _ => null,
        };
    }

    private static string ToCamelCase(string pascalName) =>
        pascalName.Length == 0
            ? pascalName
            : char.ToLowerInvariant(pascalName[0]) + pascalName[1..];
}
