using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailMessageTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        EmailMessage message = new()
        {
            To = "user@test.com",
            Subject = "Test Subject",
            HtmlBody = "<p>Hello</p>",
            PlainTextBody = "Hello",
            FromOverride = "sender@test.com",
        };

        message.To.ShouldBe("user@test.com");
        message.Subject.ShouldBe("Test Subject");
        message.HtmlBody.ShouldBe("<p>Hello</p>");
        message.PlainTextBody.ShouldBe("Hello");
        message.FromOverride.ShouldBe("sender@test.com");
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
        message.FromOverride.ShouldBeNull();
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
