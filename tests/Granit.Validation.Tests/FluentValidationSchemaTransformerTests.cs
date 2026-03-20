// =============================================================================
// Tests - FluentValidationSchemaTransformer
// =============================================================================
// Verifies:
//   - MaxLength → schema.MaxLength
//   - MinLength → schema.MinLength
//   - NotEmpty → required
//   - GreaterThan → schema.ExclusiveMinimum
//   - LessThanOrEqualTo → schema.Maximum
//   - EmailAddress → schema.Format = "email"
//   - Matches → schema.Pattern
//   - No validator → schema unchanged
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class FluentValidationSchemaTransformerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    // -------------------------------------------------------------------------
    // MaxLength → schema.MaxLength
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_MaxLength_SetsMaxLength()
    {
        OpenApiSchema schema = await TransformAsync<MaxLengthRequest, MaxLengthRequestValidator>();

        OpenApiSchema nameSchema = GetProperty(schema, "name");
        nameSchema.MaxLength.ShouldBe(100);
    }

    // -------------------------------------------------------------------------
    // NotEmpty → required
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_NotEmpty_AddsRequired()
    {
        OpenApiSchema schema = await TransformAsync<RequiredRequest, RequiredRequestValidator>();

        schema.Required.ShouldNotBeNull();
        schema.Required.ShouldContain("name");
    }

    // -------------------------------------------------------------------------
    // GreaterThan → ExclusiveMinimum
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_GreaterThan_SetsExclusiveMinimum()
    {
        OpenApiSchema schema = await TransformAsync<ComparisonRequest, ComparisonRequestValidator>();

        OpenApiSchema ageSchema = GetProperty(schema, "age");
        ageSchema.ExclusiveMinimum.ShouldBe("0");
    }

    // -------------------------------------------------------------------------
    // LessThanOrEqualTo → Maximum
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_LessThanOrEqualTo_SetsMaximum()
    {
        OpenApiSchema schema = await TransformAsync<ComparisonRequest, ComparisonRequestValidator>();

        OpenApiSchema ageSchema = GetProperty(schema, "age");
        ageSchema.Maximum.ShouldBe("150");
    }

    // -------------------------------------------------------------------------
    // EmailAddress → format: "email"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_EmailAddress_SetsFormat()
    {
        OpenApiSchema schema = await TransformAsync<EmailRequest, EmailRequestValidator>();

        OpenApiSchema emailSchema = GetProperty(schema, "email");
        emailSchema.Format.ShouldBe("email");
    }

    // -------------------------------------------------------------------------
    // Matches → pattern
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_Matches_SetsPattern()
    {
        OpenApiSchema schema = await TransformAsync<PatternRequest, PatternRequestValidator>();

        OpenApiSchema codeSchema = GetProperty(schema, "code");
        codeSchema.Pattern.ShouldBe(@"^[A-Z]{3}$");
    }

    // -------------------------------------------------------------------------
    // Matches + WithPatternHint → pattern + x-granit-pattern-hint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_MatchesWithPatternHint_SetsPatternAndHintExtension()
    {
        OpenApiSchema schema = await TransformAsync<PatternHintRequest, PatternHintRequestValidator>();

        OpenApiSchema codeSchema = GetProperty(schema, "code");
        codeSchema.Pattern.ShouldBe(@"^[A-Z]{2}$");
        codeSchema.Extensions.ShouldNotBeNull();
        codeSchema.Extensions.ShouldContainKey("x-granit-pattern-hint");
    }

    [Fact]
    public async Task TransformAsync_MatchesWithoutPatternHint_DoesNotSetHintExtension()
    {
        OpenApiSchema schema = await TransformAsync<PatternRequest, PatternRequestValidator>();

        OpenApiSchema codeSchema = GetProperty(schema, "code");
        codeSchema.Pattern.ShouldBe(@"^[A-Z]{3}$");
        if (codeSchema.Extensions is not null)
        {
            codeSchema.Extensions.ShouldNotContainKey("x-granit-pattern-hint");
        }
    }

    // -------------------------------------------------------------------------
    // No validator → schema unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TransformAsync_NoValidator_LeavesSchemaUnchanged()
    {
        OpenApiSchema schema = await TransformWithoutValidatorAsync<NoValidatorRequest>();

        OpenApiSchema nameSchema = GetProperty(schema, "name");
        nameSchema.MaxLength.ShouldBeNull();
        nameSchema.MinLength.ShouldBeNull();
        nameSchema.Pattern.ShouldBeNull();
        schema.Required.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OpenApiSchema GetProperty(OpenApiSchema schema, string propertyName)
    {
        schema.Properties.ShouldNotBeNull();
        schema.Properties.ShouldContainKey(propertyName);
        return (OpenApiSchema)schema.Properties[propertyName];
    }

    private static async Task<OpenApiSchema> TransformAsync<TRequest, TValidator>()
        where TValidator : class, IValidator<TRequest>, new()
    {
        ServiceCollection services = new();
        services.AddScoped<IValidator<TRequest>, TValidator>();
        ServiceProvider provider = services.BuildServiceProvider();

        FluentValidationSchemaTransformer transformer = new(
            provider.GetRequiredService<IServiceScopeFactory>());

        OpenApiSchema schema = CreateSchemaForType<TRequest>();
        OpenApiSchemaTransformerContext context = CreateContext<TRequest>();

        await transformer.TransformAsync(schema, context, CancellationToken.None);

        return schema;
    }

    private static async Task<OpenApiSchema> TransformWithoutValidatorAsync<TRequest>()
    {
        ServiceCollection services = new();
        ServiceProvider provider = services.BuildServiceProvider();

        FluentValidationSchemaTransformer transformer = new(
            provider.GetRequiredService<IServiceScopeFactory>());

        OpenApiSchema schema = CreateSchemaForType<TRequest>();
        OpenApiSchemaTransformerContext context = CreateContext<TRequest>();

        await transformer.TransformAsync(schema, context, CancellationToken.None);

        return schema;
    }

    private static OpenApiSchemaTransformerContext CreateContext<T>()
    {
        ServiceCollection services = new();
        ServiceProvider provider = services.BuildServiceProvider();
        JsonTypeInfo typeInfo = JsonOptions.GetTypeInfo(typeof(T));

        return new OpenApiSchemaTransformerContext
        {
            JsonTypeInfo = typeInfo,
            DocumentName = "v1",
            ParameterDescription = null,
            JsonPropertyInfo = null,
            ApplicationServices = provider,
        };
    }

    private static OpenApiSchema CreateSchemaForType<T>()
    {
        OpenApiSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>(),
        };

        foreach (System.Reflection.PropertyInfo prop in typeof(T).GetProperties())
        {
            string camelName = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
            schema.Properties[camelName] = new OpenApiSchema
            {
                Type = GetJsonSchemaType(prop.PropertyType),
            };
        }

        return schema;
    }

    private static JsonSchemaType GetJsonSchemaType(Type type) =>
        type switch
        {
            _ when type == typeof(string) => JsonSchemaType.String,
            _ when type == typeof(int) || type == typeof(long) => JsonSchemaType.Integer,
            _ when type == typeof(bool) => JsonSchemaType.Boolean,
            _ => JsonSchemaType.String,
        };

    // -------------------------------------------------------------------------
    // Test types
    // -------------------------------------------------------------------------

    private sealed record MaxLengthRequest(string Name);

    private sealed class MaxLengthRequestValidator : GranitValidator<MaxLengthRequest>
    {
        public MaxLengthRequestValidator() => RuleFor(x => x.Name).MaximumLength(100);
    }

    private sealed record RequiredRequest(string Name);

    private sealed class RequiredRequestValidator : GranitValidator<RequiredRequest>
    {
        public RequiredRequestValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    private sealed record ComparisonRequest(string Name, int Age);

    private sealed class ComparisonRequestValidator : GranitValidator<ComparisonRequest>
    {
        public ComparisonRequestValidator()
        {
            RuleFor(x => x.Age).GreaterThan(0).LessThanOrEqualTo(150);
        }
    }

    private sealed record EmailRequest(string Email);

    private sealed class EmailRequestValidator : GranitValidator<EmailRequest>
    {
        public EmailRequestValidator() => RuleFor(x => x.Email).EmailAddress();
    }

    private sealed record PatternRequest(string Code);

    private sealed class PatternRequestValidator : GranitValidator<PatternRequest>
    {
        public PatternRequestValidator() => RuleFor(x => x.Code).Matches(@"^[A-Z]{3}$");
    }

    private sealed record PatternHintRequest(string Code);

    private sealed class PatternHintRequestValidator : GranitValidator<PatternHintRequest>
    {
        public PatternHintRequestValidator() =>
            RuleFor(x => x.Code)
                .Matches(@"^[A-Z]{2}$")
                .WithPatternHint("Granit:Validation:Hints:Alpha2Code");
    }

    private sealed record NoValidatorRequest(string Name);
}
