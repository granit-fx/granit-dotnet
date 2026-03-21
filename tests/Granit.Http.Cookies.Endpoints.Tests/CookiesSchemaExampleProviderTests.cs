using System.Text.Json.Nodes;
using Granit.Http.Cookies.Endpoints.Dtos;
using Granit.Http.Cookies.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Http.Cookies.Endpoints.Tests;

public sealed class CookiesSchemaExampleProviderTests
{
    private readonly CookiesSchemaExampleProvider _sut = new();

    [Fact]
    public void GetExamples_ContainsCookieConsentConfigResponse()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _sut.GetExamples();

        examples.ShouldContainKey(typeof(CookieConsentConfigResponse));
    }

    [Fact]
    public void GetExamples_ExampleHasCookiesArray()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _sut.GetExamples();
        JsonNode example = examples[typeof(CookieConsentConfigResponse)];
        JsonObject obj = example.ShouldBeOfType<JsonObject>();

        obj["cookies"].ShouldNotBeNull();
        obj["cookies"].ShouldBeOfType<JsonArray>();
    }

    [Fact]
    public void GetExamples_ExampleHasServicesArray()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _sut.GetExamples();
        JsonNode example = examples[typeof(CookieConsentConfigResponse)];
        JsonObject obj = example.ShouldBeOfType<JsonObject>();

        obj["services"].ShouldNotBeNull();
        obj["services"].ShouldBeOfType<JsonArray>();
    }

    [Fact]
    public void GetExamples_CookiesArrayHasExpectedStructure()
    {
        IReadOnlyDictionary<Type, JsonNode> examples = _sut.GetExamples();
        JsonNode example = examples[typeof(CookieConsentConfigResponse)];
        JsonArray cookies = example["cookies"]!.AsArray();

        cookies.Count.ShouldBeGreaterThanOrEqualTo(1);
        JsonObject firstCookie = cookies[0]!.AsObject();
        firstCookie["name"].ShouldNotBeNull();
        firstCookie["category"].ShouldNotBeNull();
        firstCookie["retentionDays"].ShouldNotBeNull();
        firstCookie["purpose"].ShouldNotBeNull();
    }
}
