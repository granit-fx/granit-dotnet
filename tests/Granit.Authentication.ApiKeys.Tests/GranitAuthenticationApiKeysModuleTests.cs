using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class GranitAuthenticationApiKeysModuleTests
{
    [Fact]
    public void GranitAuthenticationApiKeysModule_IsGranitModule() =>
        typeof(GranitAuthenticationApiKeysModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();
}
