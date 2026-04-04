using Granit.Tax.Endpoints;
using Shouldly;
using Xunit;

namespace Granit.Tax.Endpoints.Tests;

public sealed class TaxEndpointsModuleTests
{
    [Fact]
    public void GranitTaxEndpointsModule_should_be_instantiable()
    {
        var module = new GranitTaxEndpointsModule();
        module.ShouldNotBeNull();
    }
}
