using System.Text.Json.Nodes;
using Granit.DataProtection;
using Granit.Mcp.Sanitization;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Shouldly;

namespace Granit.Mcp.Tests.Sanitization;

public sealed class PropertyRedactionSanitizerTests
{
    private static PropertyRedactionSanitizer CreateSanitizer() =>
        new(new SensitivePropertyRegistry([]));

    private static IServiceProvider Services => Substitute.For<IServiceProvider>();

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("userPassword")]
    [InlineData("secret")]
    [InlineData("clientSecret")]
    [InlineData("apiKey")]
    [InlineData("ApiKey")]
    [InlineData("api_key")]
    [InlineData("token")]
    [InlineData("accessToken")]
    [InlineData("connectionString")]
    [InlineData("ConnectionString")]
    [InlineData("connection_string")]
    [InlineData("credential")]
    [InlineData("ssn")]
    [InlineData("socialSecurity")]
    public async Task SanitizeAsync_WellKnownName_OmitsProperty(string propertyName)
    {
        PropertyRedactionSanitizer sut = CreateSanitizer();
        string json = $$"""{"{{propertyName}}":"sensitive-value","name":"John"}""";
        CallToolResult result = new() { Content = [new TextContentBlock { Text = json }] };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Services, TestContext.Current.CancellationToken);

        TextContentBlock block = sanitized.Content.Single().ShouldBeOfType<TextContentBlock>();
        JsonObject node = JsonNode.Parse(block.Text)!.AsObject();
        node.ContainsKey(propertyName).ShouldBeFalse();
        node["name"]!.GetValue<string>().ShouldBe("John");
    }

    [Fact]
    public async Task SanitizeAsync_NonSensitiveProperty_PreservesIt()
    {
        PropertyRedactionSanitizer sut = CreateSanitizer();
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = """{"name":"Alice","age":30}""" }],
        };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Services, TestContext.Current.CancellationToken);

        sanitized.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task SanitizeAsync_NonJsonText_ReturnsOriginal()
    {
        PropertyRedactionSanitizer sut = CreateSanitizer();
        CallToolResult result = new()
        {
            Content = [new TextContentBlock { Text = "plain text response" }],
        };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Services, TestContext.Current.CancellationToken);

        sanitized.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task SanitizeAsync_EmptyContent_ReturnsOriginal()
    {
        PropertyRedactionSanitizer sut = CreateSanitizer();
        CallToolResult result = new() { Content = [] };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Services, TestContext.Current.CancellationToken);

        sanitized.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task SanitizeAsync_NestedObject_RedactsNestedWellKnownNames()
    {
        PropertyRedactionSanitizer sut = CreateSanitizer();
        const string json = """{"user":{"name":"Alice","password":"s3cr3t"}}""";
        CallToolResult result = new() { Content = [new TextContentBlock { Text = json }] };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Services, TestContext.Current.CancellationToken);

        TextContentBlock block = sanitized.Content.Single().ShouldBeOfType<TextContentBlock>();
        JsonObject root = JsonNode.Parse(block.Text)!.AsObject();
        JsonObject user = root["user"]!.AsObject();
        user.ContainsKey("password").ShouldBeFalse();
        user["name"]!.GetValue<string>().ShouldBe("Alice");
    }

    [Fact]
    public async Task SanitizeAsync_ArrayOfObjects_RedactsWellKnownNames()
    {
        PropertyRedactionSanitizer sut = CreateSanitizer();
        const string json = """[{"name":"Alice","token":"abc"},{"name":"Bob","token":"xyz"}]""";
        CallToolResult result = new() { Content = [new TextContentBlock { Text = json }] };

        CallToolResult sanitized = await sut.SanitizeAsync(result, Services, TestContext.Current.CancellationToken);

        TextContentBlock block = sanitized.Content.Single().ShouldBeOfType<TextContentBlock>();
        JsonArray arr = JsonNode.Parse(block.Text)!.AsArray();
        arr[0]!.AsObject().ContainsKey("token").ShouldBeFalse();
        arr[1]!.AsObject().ContainsKey("token").ShouldBeFalse();
    }

    [Fact]
    public void HashValue_ProducesConsistentSha256Prefix()
    {
        string hash = PropertyRedactionSanitizer.HashValue("test");

        hash.ShouldStartWith("sha256:");
        hash.Length.ShouldBe(7 + 16); // "sha256:" + 16 hex chars
    }

    [Fact]
    public void HashValue_SameInput_SameOutput()
    {
        string h1 = PropertyRedactionSanitizer.HashValue("hello");
        string h2 = PropertyRedactionSanitizer.HashValue("hello");

        h1.ShouldBe(h2);
    }

    [Fact]
    public void HashValue_DifferentInput_DifferentOutput()
    {
        string h1 = PropertyRedactionSanitizer.HashValue("hello");
        string h2 = PropertyRedactionSanitizer.HashValue("world");

        h1.ShouldNotBe(h2);
    }

    [Fact]
    public void MaskValue_EmptyValue_ReturnsOpaqueMarker() =>
        PropertyRedactionSanitizer.MaskValue(string.Empty).ShouldBe("***");

    [Fact]
    public void MaskValue_NeverDisclosesPlaintextCharacters()
    {
        // SECURITY: redacted values must never echo any plaintext characters,
        // since prefixes/suffixes alone identify secret types (Bearer ey…, gk_…,
        // AKIA…) and shrink brute-force search space.
        const string secret = "gk_live_sk_abcdefghijABCDEF";
        string masked = PropertyRedactionSanitizer.MaskValue(secret);

        foreach (char c in secret.Distinct())
        {
            if (c is not '*' and not '[' and not ']' and not (>= '0' and <= '9'))
            {
                masked.ShouldNotContain(c.ToString());
            }
        }
    }

    [Fact]
    public void MaskValue_PreservesLengthInformationOnly()
    {
        PropertyRedactionSanitizer.MaskValue("abcdef").ShouldBe("***[6]");
        PropertyRedactionSanitizer.MaskValue("abcd").ShouldBe("***[4]");
    }
}
