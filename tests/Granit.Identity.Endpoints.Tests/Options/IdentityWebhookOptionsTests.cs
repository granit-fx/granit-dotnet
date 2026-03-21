using Granit.Identity.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Options;

public sealed class IdentityWebhookOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityWebhook() => IdentityWebhookOptions.SectionName.ShouldBe("IdentityWebhook");

    [Fact]
    public void Defaults_SecretIsEmpty()
    {
        IdentityWebhookOptions options = new();

        options.Secret.ShouldBe(string.Empty);
    }

    [Fact]
    public void Defaults_SignatureHeaderName()
    {
        IdentityWebhookOptions options = new();

        options.SignatureHeaderName.ShouldBe("X-Webhook-Signature");
    }

    [Fact]
    public void Properties_AreSettable()
    {
        IdentityWebhookOptions options = new()
        {
            Secret = "my-secret-key",
            SignatureHeaderName = "X-Custom-Signature",
        };

        options.Secret.ShouldBe("my-secret-key");
        options.SignatureHeaderName.ShouldBe("X-Custom-Signature");
    }
}
