using Shouldly;
using Xunit;

namespace Granit.Tax.Tests;

public sealed class TaxModuleTests
{
    [Fact]
    public void GranitTaxModule_should_be_instantiable()
    {
        var module = new GranitTaxModule();
        module.ShouldNotBeNull();
    }
}
