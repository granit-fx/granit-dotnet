using Granit.Entities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class EntityModuleResolverTests
{
    [Theory]
    [InlineData("Granit.Parties.Party", "Parties")]
    [InlineData("Granit.Invoicing.Invoice", "Invoicing")]
    [InlineData("Showcase.Customer", "Showcase")]
    [InlineData("Showcase.Modules.Crm.Lead", "Crm")]
    public void Resolve_returns_module_segment(string entityName, string expected)
    {
        EntityModuleResolver.Resolve(entityName).ShouldBe(expected);
    }

    [Fact]
    public void Resolve_falls_back_to_underscore_for_single_segment()
    {
        EntityModuleResolver.Resolve("Bare").ShouldBe("_");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_throws_on_empty(string raw)
    {
        Should.Throw<ArgumentException>(() => EntityModuleResolver.Resolve(raw));
    }
}
