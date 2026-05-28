using Granit.Webhooks.Internal;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class WebhookSecretHintTests
{
    [Fact]
    public void From_PreservesKnownPrefix_AndMasksBody()
    {
        // 64 hex chars after the prefix → standard whsec_ output.
        string plaintext = "whsec_b46a0123456789abcdef0123456789abcdef0123456789abcdef0123455182";

        string hint = WebhookSecretHint.From(plaintext);

        hint.ShouldStartWith("whsec_b46a");
        hint.ShouldEndWith("5182");
        hint.Length.ShouldBe(6 + 4 + 16 + 4); // prefix + first4 + 16 stars + last4
        hint.ShouldBe("whsec_b46a****************5182");
    }

    [Fact]
    public void From_IsIdempotentInShape_AcrossDifferentInputs()
    {
        string a = WebhookSecretHint.From("whsec_" + new string('a', 64));
        string b = WebhookSecretHint.From("whsec_" + new string('b', 64));

        a.Length.ShouldBe(b.Length);
        a.ShouldBe("whsec_aaaa****************aaaa");
        b.ShouldBe("whsec_bbbb****************bbbb");
    }

    [Fact]
    public void From_WithoutKnownPrefix_DoesNotInventOne()
    {
        string plaintext = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        string hint = WebhookSecretHint.From(plaintext);

        hint.ShouldNotStartWith("whsec_");
        hint.ShouldStartWith("0123");
        hint.ShouldEndWith("cdef");
        hint.Length.ShouldBe(4 + 16 + 4);
    }

    [Fact]
    public void From_ShortBody_MasksEntireBody_NoLeak()
    {
        // 8 body chars or fewer → entire body becomes asterisks so first/last don't overlap.
        string hint = WebhookSecretHint.From("whsec_abcd");

        hint.ShouldBe("whsec_****");
    }

    [Fact]
    public void From_ExactlyEightBodyChars_MasksEverything()
    {
        string hint = WebhookSecretHint.From("whsec_12345678");

        hint.ShouldBe("whsec_********");
    }

    [Fact]
    public void From_NineBodyChars_KeepsEdgesAndMasksMiddle()
    {
        string hint = WebhookSecretHint.From("whsec_123456789");

        hint.ShouldBe("whsec_1234****************6789");
    }

    [Fact]
    public void From_NullOrEmpty_Throws()
    {
        Should.Throw<ArgumentException>(() => WebhookSecretHint.From(null!));
        Should.Throw<ArgumentException>(() => WebhookSecretHint.From(string.Empty));
    }
}
