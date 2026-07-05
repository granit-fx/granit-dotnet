// =============================================================================
// Tests - JsonSchemaWriter
// =============================================================================
// Verifies the JSON Schema Draft 7 projection of FluentValidation rules:
//   - No validator → null
//   - NotEmpty → required[]
//   - MaximumLength → maxLength
//   - MinimumLength → minLength
//   - Length(min,max) → both
//   - GreaterThan → exclusiveMinimum
//   - GreaterThanOrEqualTo → minimum
//   - LessThan → exclusiveMaximum
//   - LessThanOrEqualTo → maximum
//   - InclusiveBetween → minimum + maximum
//   - Matches → pattern
//   - EmailAddress → format: "email"
//   - Pattern + WithPatternHint → x-granit-pattern-hint
//   - Custom Granit error code → x-granit-validator
//   - Multiple properties + multiple constraints per property
// =============================================================================

using System.Text.Json.Nodes;
using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.JsonSchema;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class JsonSchemaWriterTests
{
    [Fact]
    public void Write_NoValidatorRegistered_ReturnsNull()
    {
        JsonSchemaWriter writer = CreateWriter(registerValidator: false);

        JsonObject? schema = writer.Write(typeof(NoValidatorRequest));

        schema.ShouldBeNull();
    }

    [Fact]
    public void Write_NotEmpty_AddsToRequiredArray()
    {
        JsonSchemaWriter writer = CreateWriter<RequiredRequest, RequiredRequestValidator>();

        JsonObject schema = writer.Write(typeof(RequiredRequest))!;

        JsonArray required = schema["required"]!.AsArray();
        required.Select(n => n!.GetValue<string>()).ShouldContain("name");
    }

    [Fact]
    public void Write_ConditionalRuleForEach_DoesNotMarkCollectionRequired()
    {
        // Regression: a per-element RuleForEach(...).NotEmpty() guarded by .When(... is not null)
        // is neither unconditional nor a presence rule on the collection — it must NOT promote the
        // collection to `required` (OpenAPI has no conditional-required). The sibling unconditional
        // NotEmpty on a scalar still becomes required.
        JsonSchemaWriter writer = CreateWriter<ConditionalCollectionRequest, ConditionalCollectionRequestValidator>();

        JsonObject schema = writer.Write(typeof(ConditionalCollectionRequest))!;

        IEnumerable<string> required = schema["required"]!.AsArray().Select(n => n!.GetValue<string>());
        required.ShouldContain("name");
        required.ShouldNotContain("tags");
    }

    [Fact]
    public void Write_MaximumLength_SetsMaxLength()
    {
        JsonSchemaWriter writer = CreateWriter<MaxLengthRequest, MaxLengthRequestValidator>();

        JsonObject schema = writer.Write(typeof(MaxLengthRequest))!;

        schema["properties"]!["name"]!["maxLength"]!.GetValue<int>().ShouldBe(100);
    }

    [Fact]
    public void Write_MinimumLength_SetsMinLength()
    {
        JsonSchemaWriter writer = CreateWriter<MinLengthRequest, MinLengthRequestValidator>();

        JsonObject schema = writer.Write(typeof(MinLengthRequest))!;

        schema["properties"]!["name"]!["minLength"]!.GetValue<int>().ShouldBe(3);
    }

    [Fact]
    public void Write_Length_SetsBothMinAndMax()
    {
        JsonSchemaWriter writer = CreateWriter<LengthRequest, LengthRequestValidator>();

        JsonObject schema = writer.Write(typeof(LengthRequest))!;

        JsonNode prop = schema["properties"]!["code"]!;
        prop["minLength"]!.GetValue<int>().ShouldBe(2);
        prop["maxLength"]!.GetValue<int>().ShouldBe(8);
    }

    [Fact]
    public void Write_GreaterThan_SetsExclusiveMinimum()
    {
        JsonSchemaWriter writer = CreateWriter<GreaterThanRequest, GreaterThanRequestValidator>();

        JsonObject schema = writer.Write(typeof(GreaterThanRequest))!;

        schema["properties"]!["age"]!["exclusiveMinimum"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public void Write_GreaterThanOrEqualTo_SetsMinimum()
    {
        JsonSchemaWriter writer = CreateWriter<GreaterThanOrEqualRequest, GreaterThanOrEqualRequestValidator>();

        JsonObject schema = writer.Write(typeof(GreaterThanOrEqualRequest))!;

        schema["properties"]!["age"]!["minimum"]!.GetValue<int>().ShouldBe(18);
    }

    [Fact]
    public void Write_LessThan_SetsExclusiveMaximum()
    {
        JsonSchemaWriter writer = CreateWriter<LessThanRequest, LessThanRequestValidator>();

        JsonObject schema = writer.Write(typeof(LessThanRequest))!;

        schema["properties"]!["age"]!["exclusiveMaximum"]!.GetValue<int>().ShouldBe(120);
    }

    [Fact]
    public void Write_LessThanOrEqualTo_SetsMaximum()
    {
        JsonSchemaWriter writer = CreateWriter<LessThanOrEqualRequest, LessThanOrEqualRequestValidator>();

        JsonObject schema = writer.Write(typeof(LessThanOrEqualRequest))!;

        schema["properties"]!["age"]!["maximum"]!.GetValue<int>().ShouldBe(150);
    }

    [Fact]
    public void Write_InclusiveBetween_SetsMinimumAndMaximum()
    {
        JsonSchemaWriter writer = CreateWriter<BetweenRequest, BetweenRequestValidator>();

        JsonObject schema = writer.Write(typeof(BetweenRequest))!;

        JsonNode prop = schema["properties"]!["score"]!;
        prop["minimum"]!.GetValue<int>().ShouldBe(0);
        prop["maximum"]!.GetValue<int>().ShouldBe(100);
    }

    [Fact]
    public void Write_Matches_SetsPattern()
    {
        JsonSchemaWriter writer = CreateWriter<PatternRequest, PatternRequestValidator>();

        JsonObject schema = writer.Write(typeof(PatternRequest))!;

        schema["properties"]!["code"]!["pattern"]!.GetValue<string>().ShouldBe("^[A-Z]{3}$");
    }

    [Fact]
    public void Write_EmailAddress_SetsFormatEmail()
    {
        JsonSchemaWriter writer = CreateWriter<EmailRequest, EmailRequestValidator>();

        JsonObject schema = writer.Write(typeof(EmailRequest))!;

        schema["properties"]!["email"]!["format"]!.GetValue<string>().ShouldBe("email");
    }

    [Fact]
    public void Write_PatternWithHint_AddsXGranitPatternHint()
    {
        JsonSchemaWriter writer = CreateWriter<PatternHintRequest, PatternHintRequestValidator>();

        JsonObject schema = writer.Write(typeof(PatternHintRequest))!;

        JsonNode prop = schema["properties"]!["code"]!;
        prop["pattern"]!.GetValue<string>().ShouldBe("^[A-Z]{2}$");
        prop["x-granit-pattern-hint"]!.GetValue<string>().ShouldBe("Validation:Hint:Alpha2Code");
    }

    [Fact]
    public void Write_PatternWithoutHint_DoesNotEmitHintExtension()
    {
        JsonSchemaWriter writer = CreateWriter<PatternRequest, PatternRequestValidator>();

        JsonObject schema = writer.Write(typeof(PatternRequest))!;

        JsonObject prop = schema["properties"]!["code"]!.AsObject();
        prop.ContainsKey("x-granit-pattern-hint").ShouldBeFalse();
    }

    [Fact]
    public void Write_CustomGranitErrorCode_EmitsXGranitValidator()
    {
        JsonSchemaWriter writer = CreateWriter<CustomCodeRequest, CustomCodeRequestValidator>();

        JsonObject schema = writer.Write(typeof(CustomCodeRequest))!;

        schema["properties"]!["value"]!["x-granit-validator"]!
            .GetValue<string>()
            .ShouldBe("Validation:CustomFoo");
    }

    [Fact]
    public void Write_MultipleConstraintsOnSameProperty_EmitsAllOfThem()
    {
        JsonSchemaWriter writer = CreateWriter<CompositeRequest, CompositeRequestValidator>();

        JsonObject schema = writer.Write(typeof(CompositeRequest))!;

        JsonNode prop = schema["properties"]!["code"]!;
        prop["minLength"]!.GetValue<int>().ShouldBe(2);
        prop["maxLength"]!.GetValue<int>().ShouldBe(8);
        prop["pattern"]!.GetValue<string>().ShouldBe("^[A-Z]+$");
        schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldContain("code");
    }

    [Fact]
    public void Write_MultipleProperties_EmitsAllInProperties()
    {
        JsonSchemaWriter writer = CreateWriter<TwoFieldsRequest, TwoFieldsRequestValidator>();

        JsonObject schema = writer.Write(typeof(TwoFieldsRequest))!;

        JsonObject properties = schema["properties"]!.AsObject();
        properties.ContainsKey("name").ShouldBeTrue();
        properties.ContainsKey("age").ShouldBeTrue();
        properties["name"]!["maxLength"]!.GetValue<int>().ShouldBe(50);
        properties["age"]!["exclusiveMinimum"]!.GetValue<int>().ShouldBe(0);
    }

    [Fact]
    public void Write_NullType_Throws()
    {
        JsonSchemaWriter writer = CreateWriter(registerValidator: false);

        Should.Throw<ArgumentNullException>(() => writer.Write(null!));
    }

    [Fact]
    public void JsonSchemaWriter_Implements_IJsonSchemaWriter()
        // Sanity check — Granit.Entities and other consumers depend on the
        // interface, not the concrete implementation.
        => typeof(IJsonSchemaWriter).IsAssignableFrom(typeof(JsonSchemaWriter)).ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static JsonSchemaWriter CreateWriter<TRequest, TValidator>()
        where TValidator : class, IValidator<TRequest>, new()
    {
        ServiceCollection services = new();
        services.AddScoped<IValidator<TRequest>, TValidator>();
        ServiceProvider provider = services.BuildServiceProvider();
        return new JsonSchemaWriter(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static JsonSchemaWriter CreateWriter(bool registerValidator)
    {
        ServiceCollection services = new();
        ServiceProvider provider = services.BuildServiceProvider();
        return new JsonSchemaWriter(provider.GetRequiredService<IServiceScopeFactory>());
    }

    // -------------------------------------------------------------------------
    // Test types
    // -------------------------------------------------------------------------

    private sealed record NoValidatorRequest(string Name);

    private sealed record RequiredRequest(string Name);

    private sealed class RequiredRequestValidator : GranitValidator<RequiredRequest>
    {
        public RequiredRequestValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    private sealed record ConditionalCollectionRequest(string Name, string[]? Tags);

    private sealed class ConditionalCollectionRequestValidator : GranitValidator<ConditionalCollectionRequest>
    {
        public ConditionalCollectionRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleForEach(x => x.Tags).NotEmpty().When(x => x.Tags is not null);
        }
    }

    private sealed record MaxLengthRequest(string Name);

    private sealed class MaxLengthRequestValidator : GranitValidator<MaxLengthRequest>
    {
        public MaxLengthRequestValidator() => RuleFor(x => x.Name).MaximumLength(100);
    }

    private sealed record MinLengthRequest(string Name);

    private sealed class MinLengthRequestValidator : GranitValidator<MinLengthRequest>
    {
        public MinLengthRequestValidator() => RuleFor(x => x.Name).MinimumLength(3);
    }

    private sealed record LengthRequest(string Code);

    private sealed class LengthRequestValidator : GranitValidator<LengthRequest>
    {
        public LengthRequestValidator() => RuleFor(x => x.Code).Length(2, 8);
    }

    private sealed record GreaterThanRequest(int Age);

    private sealed class GreaterThanRequestValidator : GranitValidator<GreaterThanRequest>
    {
        public GreaterThanRequestValidator() => RuleFor(x => x.Age).GreaterThan(0);
    }

    private sealed record GreaterThanOrEqualRequest(int Age);

    private sealed class GreaterThanOrEqualRequestValidator : GranitValidator<GreaterThanOrEqualRequest>
    {
        public GreaterThanOrEqualRequestValidator() => RuleFor(x => x.Age).GreaterThanOrEqualTo(18);
    }

    private sealed record LessThanRequest(int Age);

    private sealed class LessThanRequestValidator : GranitValidator<LessThanRequest>
    {
        public LessThanRequestValidator() => RuleFor(x => x.Age).LessThan(120);
    }

    private sealed record LessThanOrEqualRequest(int Age);

    private sealed class LessThanOrEqualRequestValidator : GranitValidator<LessThanOrEqualRequest>
    {
        public LessThanOrEqualRequestValidator() => RuleFor(x => x.Age).LessThanOrEqualTo(150);
    }

    private sealed record BetweenRequest(int Score);

    private sealed class BetweenRequestValidator : GranitValidator<BetweenRequest>
    {
        public BetweenRequestValidator() => RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }

    private sealed record PatternRequest(string Code);

    private sealed class PatternRequestValidator : GranitValidator<PatternRequest>
    {
        public PatternRequestValidator() => RuleFor(x => x.Code).Matches("^[A-Z]{3}$");
    }

    private sealed record EmailRequest(string Email);

    private sealed class EmailRequestValidator : GranitValidator<EmailRequest>
    {
        public EmailRequestValidator() => RuleFor(x => x.Email).EmailAddress();
    }

    private sealed record PatternHintRequest(string Code);

    private sealed class PatternHintRequestValidator : GranitValidator<PatternHintRequest>
    {
        public PatternHintRequestValidator() =>
            RuleFor(x => x.Code)
                .Matches("^[A-Z]{2}$")
                .WithPatternHint("Validation:Hint:Alpha2Code");
    }

    private sealed record CustomCodeRequest(string Value);

    private sealed class CustomCodeRequestValidator : GranitValidator<CustomCodeRequest>
    {
        public CustomCodeRequestValidator() =>
            RuleFor(x => x.Value)
                .Must(v => v?.StartsWith('X') == true)
                .WithErrorCode("Validation:CustomFoo");
    }

    private sealed record CompositeRequest(string Code);

    private sealed class CompositeRequestValidator : GranitValidator<CompositeRequest>
    {
        public CompositeRequestValidator() =>
            RuleFor(x => x.Code)
                .NotEmpty()
                .Length(2, 8)
                .Matches("^[A-Z]+$");
    }

    private sealed record TwoFieldsRequest(string Name, int Age);

    private sealed class TwoFieldsRequestValidator : GranitValidator<TwoFieldsRequest>
    {
        public TwoFieldsRequestValidator()
        {
            RuleFor(x => x.Name).MaximumLength(50);
            RuleFor(x => x.Age).GreaterThan(0);
        }
    }
}
