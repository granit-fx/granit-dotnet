using Shouldly;
using Xunit;

namespace Granit.Tax.Builtin.Tests;

public sealed class TaxBuiltinModuleTests
{
    [Fact]
    public void GranitTaxBuiltinModule_should_be_instantiable()
    {
        var module = new GranitTaxBuiltinModule();
        module.ShouldNotBeNull();
    }
}
