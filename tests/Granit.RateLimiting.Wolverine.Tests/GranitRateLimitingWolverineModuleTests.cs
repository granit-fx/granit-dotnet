using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Wolverine.Tests;

public sealed class GranitRateLimitingWolverineModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitRateLimitingWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_IsGranitModule() => new GranitRateLimitingWolverineModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_DependsOnCore()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitRateLimitingWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = [.. attrs.SelectMany(a => a.DependedTypes)];

        dependedTypes.ShouldContain(typeof(Granit.RateLimiting.GranitRateLimitingModule));
    }
}
