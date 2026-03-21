using Granit.DataExchange.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests.Permissions;

public sealed class DataExchangePermissionsTests
{
    [Fact]
    public void GroupName_IsDataExchange() =>
        DataExchangePermissions.GroupName.ShouldBe("DataExchange");

    // ── Imports ──────────────────────────────────────────────────

    [Fact]
    public void Imports_Read_FollowsThreeSegmentConvention() =>
        DataExchangePermissions.Imports.Read.ShouldBe("DataExchange.Imports.Read");

    [Fact]
    public void Imports_Execute_FollowsThreeSegmentConvention() =>
        DataExchangePermissions.Imports.Execute.ShouldBe("DataExchange.Imports.Execute");

    // ── Exports ──────────────────────────────────────────────────

    [Fact]
    public void Exports_Read_FollowsThreeSegmentConvention() =>
        DataExchangePermissions.Exports.Read.ShouldBe("DataExchange.Exports.Read");

    [Fact]
    public void Exports_Execute_FollowsThreeSegmentConvention() =>
        DataExchangePermissions.Exports.Execute.ShouldBe("DataExchange.Exports.Execute");

    // ── Three-segment validation ─────────────────────────────────

    [Theory]
    [InlineData("DataExchange.Imports.Read")]
    [InlineData("DataExchange.Imports.Execute")]
    [InlineData("DataExchange.Exports.Read")]
    [InlineData("DataExchange.Exports.Execute")]
    public void AllPermissions_HaveThreeDotSeparatedSegments(string permission)
    {
        string[] segments = permission.Split('.');
        segments.Length.ShouldBe(3);
        segments[0].ShouldBe("DataExchange");
    }
}
