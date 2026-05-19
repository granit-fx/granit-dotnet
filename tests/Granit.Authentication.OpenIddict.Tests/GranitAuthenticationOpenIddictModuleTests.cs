using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authentication.OpenIddict.Tests;

public sealed class GranitAuthenticationOpenIddictModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitAuthenticationOpenIddictModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitAuthenticationOpenIddictModule).BaseType.ShouldBe(typeof(GranitModule));
}
