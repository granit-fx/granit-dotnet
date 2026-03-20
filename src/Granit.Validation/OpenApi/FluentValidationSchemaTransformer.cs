using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Granit.Validation.OpenApi;

/// <summary>
/// OpenAPI schema transformer that enriches schemas with validation constraints
/// extracted from registered <see cref="IValidator{T}"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Maps FluentValidation rules to standard OpenAPI schema properties:
/// <c>maxLength</c>, <c>minLength</c>, <c>pattern</c>, <c>minimum</c>, <c>maximum</c>,
/// <c>required</c>, and <c>format</c>.
/// </para>
/// <para>
/// Custom domain validators (IBAN, E.164, etc.) that have no standard OpenAPI equivalent
/// are exposed as <c>x-granit-validator</c> extension properties containing the structured
/// error code (e.g. <c>Granit:Validation:InvalidIban</c>).
/// </para>
/// <para>
/// Async validators (<c>MustAsync</c>) and complex custom validators without static
/// constraints are silently ignored — they have no declarative OpenAPI equivalent.
/// </para>
/// </remarks>
internal sealed class FluentValidationSchemaTransformer(
    IServiceScopeFactory scopeFactory) : IOpenApiSchemaTransformer
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorTypeCache = new();

    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        Type schemaType = context.JsonTypeInfo.Type;

        Type validatorType = ValidatorTypeCache.GetOrAdd(
            schemaType,
            static t => typeof(IValidator<>).MakeGenericType(t));

        using IServiceScope scope = scopeFactory.CreateScope();

        if (scope.ServiceProvider.GetService(validatorType) is not IValidator validator)
        {
            return Task.CompletedTask;
        }

        IValidatorDescriptor descriptor = validator.CreateDescriptor();

        if (schema.Properties is null || schema.Properties.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (KeyValuePair<string, IOpenApiSchema> property in schema.Properties)
        {
            if (property.Value is not OpenApiSchema propertySchema)
            {
                continue;
            }

            // OpenAPI uses camelCase, FluentValidation uses PascalCase
            string pascalName = char.ToUpperInvariant(property.Key[0]) + property.Key[1..];

            string? patternHint = null;

            foreach ((IPropertyValidator propertyValidator, IRuleComponent component)
                in descriptor.GetValidatorsForMember(pascalName))
            {
                if (propertyValidator is IPatternHintProvider hintProvider)
                {
                    patternHint = hintProvider.HintKey;
                    continue;
                }

                ApplyConstraint(schema, propertySchema, propertyValidator, component, property.Key);
            }

            if (patternHint is not null && propertySchema.Pattern is not null)
            {
                propertySchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                propertySchema.Extensions["x-granit-pattern-hint"] =
                    new JsonNodeExtension(JsonValue.Create(patternHint)!);
            }
        }

        return Task.CompletedTask;
    }

    private static void ApplyConstraint(
        OpenApiSchema parentSchema,
        OpenApiSchema propertySchema,
        IPropertyValidator validator,
        IRuleComponent component,
        string propertyName)
    {
        switch (validator)
        {
            case INotNullValidator or INotEmptyValidator:
                parentSchema.Required ??= new HashSet<string>();
                parentSchema.Required.Add(propertyName);
                break;

            case IMaximumLengthValidator maxLength:
                propertySchema.MaxLength = maxLength.Max;
                break;

            case IMinimumLengthValidator minLength:
                propertySchema.MinLength = minLength.Min;
                break;

            case ILengthValidator length:
                propertySchema.MinLength = length.Min;
                propertySchema.MaxLength = length.Max;
                break;

            case IBetweenValidator between:
                if (between.From is IConvertible fromValue)
                {
                    propertySchema.Minimum = fromValue.ToString(CultureInfo.InvariantCulture);
                }

                if (between.To is IConvertible toValue)
                {
                    propertySchema.Maximum = toValue.ToString(CultureInfo.InvariantCulture);
                }

                break;

            case IComparisonValidator comparison:
                ApplyComparisonConstraint(propertySchema, comparison);
                break;

            case IRegularExpressionValidator regex:
                propertySchema.Pattern = regex.Expression;
                break;

            case IEmailValidator:
                propertySchema.Format = "email";
                break;

            default:
                // Custom Granit validators — expose via extension using the error code
                string? errorCode = component.ErrorCode;
                if (errorCode?.StartsWith("Granit:Validation:", StringComparison.Ordinal) == true)
                {
                    propertySchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    propertySchema.Extensions["x-granit-validator"] =
                        new JsonNodeExtension(JsonValue.Create(errorCode)!);
                }

                break;
        }
    }

    private static void ApplyComparisonConstraint(
        OpenApiSchema propertySchema,
        IComparisonValidator comparison)
    {
        if (comparison.ValueToCompare is not IConvertible convertible)
        {
            return;
        }

        string value = convertible.ToString(CultureInfo.InvariantCulture);

        switch (comparison.Comparison)
        {
            case Comparison.GreaterThanOrEqual:
                propertySchema.Minimum = value;
                break;
            case Comparison.GreaterThan:
                propertySchema.ExclusiveMinimum = value;
                break;
            case Comparison.LessThanOrEqual:
                propertySchema.Maximum = value;
                break;
            case Comparison.LessThan:
                propertySchema.ExclusiveMaximum = value;
                break;
        }
    }
}
