using Shouldly;
using Xunit;

namespace Granit.Tax.Internal.Tests;

public sealed class TaxInternalModuleTests
{
    [Fact]
    public void GranitTaxInternalModule_should_be_instantiable()
    {
        var module = new GranitTaxInternalModule();
        module.ShouldNotBeNull();
    }
}
