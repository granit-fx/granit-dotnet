using Granit.Notifications.WebPush.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class WebPushChannelOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        WebPushChannelOptions.SectionName.ShouldBe("Notifications:WebPush");

    [Fact]
    public void Defaults_VapidSubjectIsEmpty()
    {
        WebPushChannelOptions options = new();

        options.VapidSubject.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_VapidPublicKeyIsEmpty()
    {
        WebPushChannelOptions options = new();

        options.VapidPublicKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_VapidPrivateKeyIsEmpty()
    {
        WebPushChannelOptions options = new();

        options.VapidPrivateKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        WebPushChannelOptions options = new()
        {
            VapidSubject = "mailto:test@example.com",
            VapidPublicKey = "public-key",
            VapidPrivateKey = "private-key",
        };

        options.VapidSubject.ShouldBe("mailto:test@example.com");
        options.VapidPublicKey.ShouldBe("public-key");
        options.VapidPrivateKey.ShouldBe("private-key");
    }
}
