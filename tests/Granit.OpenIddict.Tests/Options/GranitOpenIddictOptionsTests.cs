using Granit.OpenIddict.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Options;

public sealed class GranitOpenIddictOptionsTests
{
    [Fact]
    public void SectionName_IsOpenIddict() =>
        GranitOpenIddictOptions.SectionName.ShouldBe("OpenIddict");
}
