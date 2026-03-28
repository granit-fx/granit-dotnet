using Granit.Oidc.TokenManagement.Options;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests.Options;

public sealed class TokenManagementOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        TokenManagementOptions.SectionName.ShouldBe("TokenManagement");

    [Fact]
    public void DefaultCacheMargin_DefaultsTo30Seconds() =>
        new TokenManagementOptions().DefaultCacheMargin.ShouldBe(TimeSpan.FromSeconds(30));

    [Fact]
    public void DefaultCacheMargin_CanBeSet()
    {
        var options = new TokenManagementOptions { DefaultCacheMargin = TimeSpan.FromMinutes(1) };

        options.DefaultCacheMargin.ShouldBe(TimeSpan.FromMinutes(1));
    }
}
