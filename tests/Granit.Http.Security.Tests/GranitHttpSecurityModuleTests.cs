using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Http.Security.Tests;

public sealed class GranitHttpSecurityModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitHttpSecurityModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitHttpSecurityModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();
}
