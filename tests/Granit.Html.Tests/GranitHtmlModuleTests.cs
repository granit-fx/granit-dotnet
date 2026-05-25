using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Html.Tests;

public sealed class GranitHtmlModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule() =>
        new GranitHtmlModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitHtmlModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_declares_no_DependsOn()
    {
        // Pure contracts package — no service registrations, no dependencies beyond
        // the implicit Granit base. Adding a [DependsOn] would couple every consumer
        // to a transitive tree they don't need.
        object[] attrs = typeof(GranitHtmlModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: true);
        attrs.ShouldBeEmpty();
    }

    [Fact]
    public void IHtmlToPlainTextConverter_is_public()
    {
        Type t = typeof(IHtmlToPlainTextConverter);
        t.IsPublic.ShouldBeTrue();
        t.IsInterface.ShouldBeTrue();
    }
}
