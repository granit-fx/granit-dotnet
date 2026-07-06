using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class GranitFeaturesDbPropertiesTests
{
    [Fact]
    public void Default_DbTablePrefix_IsFeaturesUnderscore() => GranitFeaturesDbProperties.DbTablePrefix.ShouldBe("features_");

    [Fact]
    public void Default_DbSchema_IsNull() => GranitFeaturesDbProperties.DbSchema.ShouldBeNull();
}
