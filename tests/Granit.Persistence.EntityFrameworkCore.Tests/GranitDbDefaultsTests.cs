using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

[Collection("DbDefaults")]
public sealed class GranitDbDefaultsTests : IDisposable
{
    public GranitDbDefaultsTests() => GranitDbDefaults.ResetToDefaults();
    public void Dispose() => GranitDbDefaults.ResetToDefaults();

    [Fact]
    public void DbSchema_Default_IsNull() =>
        GranitDbDefaults.DbSchema.ShouldBeNull();

    [Fact]
    public void HostDbSchema_Default_IsNull() =>
        GranitDbDefaults.HostDbSchema.ShouldBeNull();

    [Fact]
    public void ResetToDefaults_ClearsBothProperties()
    {
        GranitDbDefaults.DbSchema = "app";
        GranitDbDefaults.HostDbSchema = "host";

        GranitDbDefaults.ResetToDefaults();

        GranitDbDefaults.DbSchema.ShouldBeNull();
        GranitDbDefaults.HostDbSchema.ShouldBeNull();
    }
}
