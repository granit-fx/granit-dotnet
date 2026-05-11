using Granit.Browsing.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Diagnostics;

public sealed class ConsoleRedactorTests
{
    [Fact]
    public void Bearer_token_should_be_redacted()
    {
        string output = ConsoleRedactor.Redact("Authorization: Bearer abc.def.ghi");

        output.ShouldContain("Bearer ***");
        output.ShouldNotContain("abc.def.ghi");
    }

    [Fact]
    public void JWT_shape_should_be_redacted_under_bearer_prefix()
    {
        string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NSJ9.SflKxwRJSMeKKF2Q";
        string output = ConsoleRedactor.Redact($"Bearer {jwt}");

        output.ShouldNotContain(jwt);
    }

    [Fact]
    public void Aws_access_key_should_be_redacted()
    {
        string output = ConsoleRedactor.Redact("found AKIAIOSFODNN7EXAMPLE in logs");

        output.ShouldNotContain("AKIAIOSFODNN7EXAMPLE");
        output.ShouldContain("***");
    }

    [Fact]
    public void Aws_secret_assignment_should_be_redacted()
    {
        string output = ConsoleRedactor.Redact("aws_secret_access_key=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");

        output.ShouldContain("aws_secret_access_key=***");
        output.ShouldNotContain("wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY");
    }

    [Fact]
    public void Gcp_api_key_should_be_redacted()
    {
        string output = ConsoleRedactor.Redact("AIzaSyA-1234567890abcdefghijklmnopqrstuvw");

        output.ShouldNotContain("AIzaSyA-1234567890abcdefghijklmnopqrstuvw");
        output.ShouldContain("***");
    }

    [Fact]
    public void Set_cookie_line_should_be_redacted()
    {
        string output = ConsoleRedactor.Redact("Set-Cookie: session=verysecret; HttpOnly");

        output.ShouldNotContain("verysecret");
        output.ShouldContain("Set-Cookie: ***");
    }

    [Fact]
    public void Basic_auth_in_url_should_be_redacted()
    {
        string output = ConsoleRedactor.Redact("connecting to https://alice:hunter2@example.com/foo");

        output.ShouldNotContain("hunter2");
        output.ShouldContain("***");
    }

    [Fact]
    public void Empty_input_should_passthrough()
    {
        ConsoleRedactor.Redact(null).ShouldBe(string.Empty);
        ConsoleRedactor.Redact(string.Empty).ShouldBe(string.Empty);
    }

    [Fact]
    public void Innocuous_input_should_passthrough()
    {
        const string Innocuous = "Document loaded in 42ms.";

        ConsoleRedactor.Redact(Innocuous).ShouldBe(Innocuous);
    }
}
