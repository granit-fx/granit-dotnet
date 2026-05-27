using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Http.RateLimiting.Tests;

public sealed class GranitHttpRateLimitingModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitHttpRateLimitingModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_IsGranitModule() => new GranitHttpRateLimitingModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_DependsOnCoreAndExceptionHandling()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitHttpRateLimitingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = [.. attrs.SelectMany(a => a.DependedTypes)];

        dependedTypes.ShouldContain(typeof(Granit.RateLimiting.GranitRateLimitingModule));
        dependedTypes.ShouldContain(typeof(Granit.Http.ExceptionHandling.GranitHttpExceptionHandlingModule));
    }
}
