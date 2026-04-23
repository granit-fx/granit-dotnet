using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.Abstractions.Tests;

public sealed class GranitDataLookupAbstractionsModuleTests
{
    [Fact]
    public void Module_is_a_GranitModule()
    {
        typeof(GranitModule).IsAssignableFrom(typeof(GranitDataLookupAbstractionsModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_is_publicly_visible()
    {
        typeof(GranitDataLookupAbstractionsModule).IsPublic.ShouldBeTrue();
    }

    [Fact]
    public void Module_is_sealed()
    {
        typeof(GranitDataLookupAbstractionsModule).IsSealed.ShouldBeTrue();
    }
}
