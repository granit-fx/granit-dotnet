using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.Wolverine.Tests;

public sealed class ModuleTests
{
    [Fact]
    public void Module_type_exists()
    {
        Type? moduleType = typeof(GranitCustomerBalanceModule).Assembly.GetType(
            "Granit.CustomerBalance.GranitCustomerBalanceModule");
        moduleType.ShouldNotBeNull();
    }
}
