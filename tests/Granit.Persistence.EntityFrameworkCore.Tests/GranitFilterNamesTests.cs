using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class GranitFilterNamesTests
{
    [Fact]
    public void SoftDelete_HasExpectedValue() => GranitFilterNames.SoftDelete.ShouldBe("SoftDelete");

    [Fact]
    public void Active_HasExpectedValue() => GranitFilterNames.Active.ShouldBe("Active");

    [Fact]
    public void ProcessingRestrictable_HasExpectedValue() => GranitFilterNames.ProcessingRestrictable.ShouldBe("ProcessingRestrictable");

    [Fact]
    public void MultiTenant_HasExpectedValue() => GranitFilterNames.MultiTenant.ShouldBe("MultiTenant");

    [Fact]
    public void Publishable_HasExpectedValue() => GranitFilterNames.Publishable.ShouldBe("Publishable");

    [Fact]
    public void AllFilterNames_AreUnique()
    {
        string[] names =
        [
            GranitFilterNames.SoftDelete,
            GranitFilterNames.Active,
            GranitFilterNames.ProcessingRestrictable,
            GranitFilterNames.MultiTenant,
            GranitFilterNames.Publishable,
        ];

        names.Distinct().Count().ShouldBe(names.Length, "All filter names must be unique");
    }
}
