using Granit.Tax.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Tax.EntityFrameworkCore.Tests;

public sealed class TaxEntityFrameworkCoreModuleTests
{
    [Fact]
    public void GranitTaxEntityFrameworkCoreModule_should_be_instantiable()
    {
        var module = new GranitTaxEntityFrameworkCoreModule();
        module.ShouldNotBeNull();
    }
}
