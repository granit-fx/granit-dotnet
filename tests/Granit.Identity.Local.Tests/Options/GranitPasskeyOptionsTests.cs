using Granit.Identity.Local.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Tests.Options;

public sealed class GranitPasskeyOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        GranitPasskeyOptions.SectionName.ShouldBe("Identity:Passkeys");

    [Fact]
    public void ServerDomain_Default_IsEmpty() =>
        new GranitPasskeyOptions().ServerDomain.ShouldBe(string.Empty);

    [Fact]
    public void AuthenticatorTimeout_Default_IsFiveMinutes() =>
        new GranitPasskeyOptions().AuthenticatorTimeout.ShouldBe(TimeSpan.FromMinutes(5));

    [Fact]
    public void ChallengeSize_Default_Is32() =>
        new GranitPasskeyOptions().ChallengeSize.ShouldBe(32);

    [Fact]
    public void ServerDomain_CanBeSet()
    {
        GranitPasskeyOptions options = new() { ServerDomain = "example.com" };
        options.ServerDomain.ShouldBe("example.com");
    }

    [Fact]
    public void AuthenticatorTimeout_CanBeSet()
    {
        GranitPasskeyOptions options = new() { AuthenticatorTimeout = TimeSpan.FromSeconds(30) };
        options.AuthenticatorTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ChallengeSize_CanBeSet()
    {
        GranitPasskeyOptions options = new() { ChallengeSize = 64 };
        options.ChallengeSize.ShouldBe(64);
    }
}
