using System.Text;
using System.Text.Json;
using Granit.Bff.Endpoints.Endpoints;
using Granit.Bff.Endpoints.Extensions;
using Granit.Bff.Options;
using Shouldly;
using Xunit;

namespace Granit.Bff.Endpoints.Tests.Endpoints;

public sealed class BffHelperMethodTests
{
    // ──── Helper: build a minimal unsigned JWT for testing ────

    private static string BuildTestJwt(object payload)
    {
        string header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        string payloadJson = JsonSerializer.Serialize(payload);
        string body = Base64UrlEncode(payloadJson);
        return $"{header}.{body}.";
    }

    private static string Base64UrlEncode(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // ──── GenerateCodeVerifier ────

    [Fact]
    public void GenerateCodeVerifier_ReturnsNonNullNonEmptyString()
    {
        string result = BffLoginEndpoints.GenerateCodeVerifier();

        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateCodeVerifier_ReturnsBase64UrlEncodedString()
    {
        string result = BffLoginEndpoints.GenerateCodeVerifier();

        result.ShouldNotContain("+");
        result.ShouldNotContain("/");
        result.ShouldNotContain("=");
    }

    [Fact]
    public void GenerateCodeVerifier_ReturnsDifferentValuesOnEachCall()
    {
        string first = BffLoginEndpoints.GenerateCodeVerifier();
        string second = BffLoginEndpoints.GenerateCodeVerifier();

        first.ShouldNotBe(second);
    }

    // ──── ComputeCodeChallenge ────

    [Fact]
    public void ComputeCodeChallenge_ReturnsNonNullNonEmptyString()
    {
        string result = BffLoginEndpoints.ComputeCodeChallenge("test-verifier");

        result.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ComputeCodeChallenge_ReturnsBase64UrlEncodedString()
    {
        string result = BffLoginEndpoints.ComputeCodeChallenge("test-verifier");

        result.ShouldNotContain("+");
        result.ShouldNotContain("/");
        result.ShouldNotContain("=");
    }

    [Fact]
    public void ComputeCodeChallenge_SameInput_ReturnsSameOutput()
    {
        string first = BffLoginEndpoints.ComputeCodeChallenge("deterministic-verifier");
        string second = BffLoginEndpoints.ComputeCodeChallenge("deterministic-verifier");

        first.ShouldBe(second);
    }

    [Fact]
    public void ComputeCodeChallenge_DifferentInput_ReturnsDifferentOutput()
    {
        string first = BffLoginEndpoints.ComputeCodeChallenge("verifier-one");
        string second = BffLoginEndpoints.ComputeCodeChallenge("verifier-two");

        first.ShouldNotBe(second);
    }

    // ──── ExtractSubFromIdToken ────

    [Fact]
    public void ExtractSubFromIdToken_ValidJwtWithSub_ReturnsSubValue()
    {
        string jwt = BuildTestJwt(new { sub = "user-123" });

        string? result = BffLoginEndpoints.ExtractSubFromIdToken(jwt);

        result.ShouldBe("user-123");
    }

    [Fact]
    public void ExtractSubFromIdToken_NullToken_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ExtractSubFromIdToken(null);

        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractSubFromIdToken_EmptyToken_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ExtractSubFromIdToken(string.Empty);

        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractSubFromIdToken_MalformedJwt_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ExtractSubFromIdToken("not-a-jwt");

        result.ShouldBeNull();
    }

    [Fact]
    public void ExtractSubFromIdToken_JwtWithoutSub_ReturnsNull()
    {
        string jwt = BuildTestJwt(new { name = "John", email = "john@example.com" });

        string? result = BffLoginEndpoints.ExtractSubFromIdToken(jwt);

        result.ShouldBeNull();
    }

    // ──── MaskSessionId (BffLoginEndpoints) ────

    [Fact]
    public void MaskSessionId_Login_LongSessionId_MasksMiddlePortion()
    {
        string result = BffLoginEndpoints.MaskSessionId("abcdefghijklmnop");

        result.ShouldStartWith("abcd");
        result.ShouldEndWith("mnop");
        result.ShouldContain("...");
    }

    [Fact]
    public void MaskSessionId_Login_ShortSessionId_ReturnsFullMask()
    {
        string result = BffLoginEndpoints.MaskSessionId("abcd1234");

        result.ShouldBe("****");
    }

    [Fact]
    public void MaskSessionId_Login_ExactlyNineChars_MasksMiddle()
    {
        string result = BffLoginEndpoints.MaskSessionId("123456789");

        result.ShouldBe("1234...6789");
    }

    // ──── MaskSessionId (BffSessionEndpoints) ────

    [Fact]
    public void MaskSessionId_Session_LongSessionId_MasksMiddlePortion()
    {
        string result = BffSessionEndpoints.MaskSessionId("abcdefghijklmnop");

        result.ShouldStartWith("abcd");
        result.ShouldEndWith("mnop");
        result.ShouldContain("...");
    }

    [Fact]
    public void MaskSessionId_Session_ShortSessionId_ReturnsFullMask()
    {
        string result = BffSessionEndpoints.MaskSessionId("short");

        result.ShouldBe("****");
    }

    // ──── BuildDirectAuthorizeUrl ────

    [Fact]
    public void BuildDirectAuthorizeUrl_IncludesAllParameters()
    {
        string result = BffLoginEndpoints.BuildDirectAuthorizeUrl(
            "https://auth.example.com",
            "my-client",
            "https://app.example.com/bff/callback",
            "openid profile",
            "random-state",
            "code-challenge-value");

        result.ShouldStartWith("https://auth.example.com/connect/authorize?");
        result.ShouldContain("client_id=my-client");
        result.ShouldContain("response_type=code");
        result.ShouldContain("redirect_uri=");
        result.ShouldContain("scope=openid%20profile");
        result.ShouldContain("state=random-state");
        result.ShouldContain("code_challenge=code-challenge-value");
        result.ShouldContain("code_challenge_method=S256");
    }

    [Fact]
    public void BuildDirectAuthorizeUrl_UrlEncodesSpecialCharacters()
    {
        string result = BffLoginEndpoints.BuildDirectAuthorizeUrl(
            "https://auth.example.com",
            "client with spaces",
            "https://app.example.com/callback",
            "openid",
            "state+value",
            "challenge");

        result.ShouldContain("client_id=client%20with%20spaces");
    }

    // ──── DecodeIdTokenClaims ────

    [Fact]
    public void DecodeIdTokenClaims_ValidJwt_ReturnsClaims()
    {
        string jwt = BuildTestJwt(new { sub = "user-456", name = "Jane Doe", email = "jane@example.com" });

        Dictionary<string, string>? result = BffUserEndpoints.DecodeIdTokenClaims(jwt);

        result.ShouldNotBeNull();
        result["sub"].ShouldBe("user-456");
        result["name"].ShouldBe("Jane Doe");
        result["email"].ShouldBe("jane@example.com");
    }

    [Fact]
    public void DecodeIdTokenClaims_NullToken_ReturnsNull()
    {
        Dictionary<string, string>? result = BffUserEndpoints.DecodeIdTokenClaims(null);

        result.ShouldBeNull();
    }

    [Fact]
    public void DecodeIdTokenClaims_EmptyToken_ReturnsNull()
    {
        Dictionary<string, string>? result = BffUserEndpoints.DecodeIdTokenClaims(string.Empty);

        result.ShouldBeNull();
    }

    [Fact]
    public void DecodeIdTokenClaims_MalformedJwt_ReturnsNull()
    {
        Dictionary<string, string>? result = BffUserEndpoints.DecodeIdTokenClaims("not.valid");

        result.ShouldBeNull();
    }

    [Fact]
    public void DecodeIdTokenClaims_SingleSegmentJwt_ReturnsNull()
    {
        Dictionary<string, string>? result = BffUserEndpoints.DecodeIdTokenClaims("single-segment");

        result.ShouldBeNull();
    }

    [Fact]
    public void DecodeIdTokenClaims_NonStringValues_ReturnsRawText()
    {
        string jwt = BuildTestJwt(new { sub = "user-789", age = 30, active = true });

        Dictionary<string, string>? result = BffUserEndpoints.DecodeIdTokenClaims(jwt);

        result.ShouldNotBeNull();
        result["age"].ShouldBe("30");
        result["active"].ShouldBe("true");
    }

    // ──── ExtractStringArray ────

    [Fact]
    public void ExtractStringArray_JsonArrayValue_ReturnsStringArray()
    {
        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase)
        {
            ["roles"] = """["admin","user","viewer"]""",
        };

        string[] result = BffUserEndpoints.ExtractStringArray(claims, "roles");

        result.Length.ShouldBe(3);
        result.ShouldContain("admin");
        result.ShouldContain("user");
        result.ShouldContain("viewer");
    }

    [Fact]
    public void ExtractStringArray_SingleStringValue_ReturnsSingleElementArray()
    {
        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase)
        {
            ["roles"] = "admin",
        };

        string[] result = BffUserEndpoints.ExtractStringArray(claims, "roles");

        result.Length.ShouldBe(1);
        result[0].ShouldBe("admin");
    }

    [Fact]
    public void ExtractStringArray_MissingKey_ReturnsEmptyArray()
    {
        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase)
        {
            ["sub"] = "user-123",
        };

        string[] result = BffUserEndpoints.ExtractStringArray(claims, "roles");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractStringArray_FallsBackToSingularKey()
    {
        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase)
        {
            ["role"] = """["editor","reader"]""",
        };

        string[] result = BffUserEndpoints.ExtractStringArray(claims, "roles");

        result.Length.ShouldBe(2);
        result.ShouldContain("editor");
        result.ShouldContain("reader");
    }

    [Fact]
    public void ExtractStringArray_SingleJsonStringValue_ReturnsSingleElementArray()
    {
        Dictionary<string, string> claims = new(StringComparer.OrdinalIgnoreCase)
        {
            ["roles"] = """  "contributor"  """,
        };

        string[] result = BffUserEndpoints.ExtractStringArray(claims, "roles");

        result.Length.ShouldBe(1);
        result[0].ShouldBe("contributor");
    }

    // ──── ValidateReturnUrl ────

    private static BffFrontendOptions CreateFrontend(string? clientUrl = null) => new()
    {
        Name = "test",
        ClientId = "test-client",
        ClientUrl = clientUrl,
    };

    [Fact]
    public void ValidateReturnUrl_RelativePath_ReturnsPath()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("/tenants", CreateFrontend());

        result.ShouldBe("/tenants");
    }

    [Fact]
    public void ValidateReturnUrl_RelativePathWithQuery_ReturnsPath()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("/tenants?page=2", CreateFrontend());

        result.ShouldBe("/tenants?page=2");
    }

    [Fact]
    public void ValidateReturnUrl_Null_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl(null, CreateFrontend());

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateReturnUrl_Empty_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("", CreateFrontend());

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateReturnUrl_Whitespace_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("   ", CreateFrontend());

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateReturnUrl_ProtocolRelativeUrl_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("//evil.com/steal", CreateFrontend());

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateReturnUrl_AbsoluteExternalUrl_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("https://evil.com/steal", CreateFrontend());

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateReturnUrl_NoLeadingSlash_ReturnsNull()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("tenants", CreateFrontend());

        result.ShouldBeNull();
    }

    [Fact]
    public void ValidateReturnUrl_RootPath_ReturnsRoot()
    {
        string? result = BffLoginEndpoints.ValidateReturnUrl("/", CreateFrontend());

        result.ShouldBe("/");
    }

    // ──── GetContentType ────

    [Theory]
    [InlineData("index.html", "text/html")]
    [InlineData("app.js", "application/javascript")]
    [InlineData("style.css", "text/css")]
    [InlineData("config.json", "application/json")]
    [InlineData("logo.png", "image/png")]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("animation.gif", "image/gif")]
    [InlineData("icon.svg", "image/svg+xml")]
    [InlineData("favicon.ico", "image/x-icon")]
    [InlineData("font.woff", "font/woff")]
    [InlineData("font.woff2", "font/woff2")]
    [InlineData("font.ttf", "font/ttf")]
    [InlineData("source.map", "application/json")]
    public void GetContentType_KnownExtension_ReturnsCorrectMimeType(string path, string expected)
    {
        string result = BffEndpointRouteBuilderExtensions.GetContentType(path);

        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("file.xyz")]
    [InlineData("file.bin")]
    [InlineData("file.unknown")]
    public void GetContentType_UnknownExtension_ReturnsOctetStream(string path)
    {
        string result = BffEndpointRouteBuilderExtensions.GetContentType(path);

        result.ShouldBe("application/octet-stream");
    }

    [Fact]
    public void GetContentType_UpperCaseExtension_ReturnsCorrectMimeType()
    {
        string result = BffEndpointRouteBuilderExtensions.GetContentType("FILE.HTML");

        result.ShouldBe("text/html");
    }
}
