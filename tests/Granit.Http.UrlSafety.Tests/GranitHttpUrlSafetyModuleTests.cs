using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Http.UrlSafety.Tests;

public sealed class GranitHttpUrlSafetyModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitHttpUrlSafetyModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitHttpUrlSafetyModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();
}
