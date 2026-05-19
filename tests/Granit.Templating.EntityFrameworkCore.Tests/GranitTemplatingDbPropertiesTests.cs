using Shouldly;
using Xunit;

namespace Granit.Templating.EntityFrameworkCore.Tests;

public sealed class GranitTemplatingDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_DefaultValue_IsTemplating() => GranitTemplatingDbProperties.DbTablePrefix.ShouldBe("templating_");

    [Fact]
    public void DbSchema_DefaultValue_IsNull() => GranitTemplatingDbProperties.DbSchema.ShouldBeNull();
}
