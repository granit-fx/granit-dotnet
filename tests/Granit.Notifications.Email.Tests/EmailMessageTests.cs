using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailMessageTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        Dictionary<string, string> headers = new()
        {
            ["List-Unsubscribe"] = "<mailto:unsubscribe@test.com>",
            ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
        };

        EmailMessage message = new()
        {
            To = "user@test.com",
            ToName = "Test User",
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            PlainTextBody = "Hello",
            FromEmailOverride = "sender@test.com",
            FromNameOverride = "Custom Sender",
            Headers = headers,
        };

        message.To.ShouldBe("user@test.com");
        message.ToName.ShouldBe("Test User");
        message.Subject.ShouldBe("Test Subject");
        message.HtmlBody.ShouldBe("<p>Hello</p>");
        message.PlainTextBody.ShouldBe("Hello");
        message.FromEmailOverride.ShouldBe("sender@test.com");
        message.FromNameOverride.ShouldBe("Custom Sender");
        message.Headers.ShouldBe(headers);
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        EmailMessage message = new()
        {
            To = "user@test.com",
            Subject = "Test",
            HtmlBody = "<p>Test</p>",
        };

        message.PlainTextBody.ShouldBeNull();
        message.FromEmailOverride.ShouldBeNull();
        message.ToName.ShouldBeNull();
        message.FromNameOverride.ShouldBeNull();
        message.Headers.ShouldBeNull();
    }

    [Fact]
    public void IsSealed() =>
        typeof(EmailMessage).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(EmailMessage).GetMethod("<Clone>$").ShouldNotBeNull();

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        EmailMessage a = new() { To = "a@test.com", Subject = "S", HtmlBody = "<p>B</p>" };
        EmailMessage b = new() { To = "a@test.com", Subject = "S", HtmlBody = "<p>B</p>" };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        EmailMessage a = new() { To = "a@test.com", Subject = "S", HtmlBody = "<p>B</p>" };
        EmailMessage b = new() { To = "b@test.com", Subject = "S", HtmlBody = "<p>B</p>" };

        a.ShouldNotBe(b);
    }
}
