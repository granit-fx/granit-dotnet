using Granit.Notifications.WebPush.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WebPush.Tests;

public sealed class PushChannelOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        PushChannelOptions.SectionName.ShouldBe("Notifications:WebPush");

    [Fact]
    public void Defaults_VapidSubjectIsEmpty()
    {
        PushChannelOptions options = new();

        options.VapidSubject.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_VapidPublicKeyIsEmpty()
    {
        PushChannelOptions options = new();

        options.VapidPublicKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_VapidPrivateKeyIsEmpty()
    {
        PushChannelOptions options = new();

        options.VapidPrivateKey.ShouldBe(string.Empty);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        PushChannelOptions options = new()
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
