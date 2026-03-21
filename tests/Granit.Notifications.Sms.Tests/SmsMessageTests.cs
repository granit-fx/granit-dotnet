using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.Tests;

public sealed class SmsMessageTests
{
    [Fact]
    public void Properties_SetCorrectly()
    {
        SmsMessage message = new()
        {
            To = "+32470123456",
            Body = "Hello World",
            SenderId = "MyApp",
        };

        message.To.ShouldBe("+32470123456");
        message.Body.ShouldBe("Hello World");
        message.SenderId.ShouldBe("MyApp");
    }

    [Fact]
    public void SenderId_DefaultsToNull()
    {
        SmsMessage message = new()
        {
            To = "+32470123456",
            Body = "Hello",
        };

        message.SenderId.ShouldBeNull();
    }

    [Fact]
    public void IsSealed() =>
        typeof(SmsMessage).IsSealed.ShouldBeTrue();

    [Fact]
    public void IsRecord() =>
        typeof(SmsMessage).GetMethod("<Clone>$").ShouldNotBeNull();

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        SmsMessage a = new() { To = "+32470123456", Body = "Hello" };
        SmsMessage b = new() { To = "+32470123456", Body = "Hello" };

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        SmsMessage a = new() { To = "+32470123456", Body = "Hello" };
        SmsMessage b = new() { To = "+32470999999", Body = "Hello" };

        a.ShouldNotBe(b);
    }
}
