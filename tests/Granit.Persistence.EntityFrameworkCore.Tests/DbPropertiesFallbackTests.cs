using Granit.BackgroundJobs.EntityFrameworkCore;
using Granit.BlobStorage.EntityFrameworkCore;
using Granit.Identity.EntityFrameworkCore;
using Granit.MultiTenancy.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies the host/tenant fallback chain on representative <c>*DbProperties</c> classes.
/// Host modules: <c>explicit → HostDbSchema → DbSchema → null</c>.
/// Tenant modules: <c>explicit → DbSchema → null</c>.
/// </summary>
[Collection("DbDefaults")]
public sealed class DbPropertiesFallbackTests : IDisposable
{
    public DbPropertiesFallbackTests() => Reset();
    public void Dispose() => Reset();

    // Reset explicit overrides by setting via the property (which sets the explicit flag).
    // Then we need to "un-set" the explicit flag. Since ResetToDefaults is only on GranitDbDefaults,
    // we rely on a fresh test process or accept that explicit flag carries across tests.
    // For now, these tests must run in isolation or use a known state.
    private static void Reset() => GranitDbDefaults.ResetToDefaults();

    // ── Host module: MultiTenancyDbProperties ─────────────────────────

    [Fact]
    public void HostModule_NoDefaults_ReturnsNull() =>
        // No global, no explicit → null
        MultiTenancyDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void HostModule_HostDbSchemaSet_ReturnsHostSchema()
    {
        GranitDbDefaults.HostDbSchema = "host";

        MultiTenancyDbProperties.DbSchema.ShouldBe("host");
    }

    [Fact]
    public void HostModule_OnlyDbSchemaSet_FallsBackToDbSchema()
    {
        GranitDbDefaults.DbSchema = "myapp";

        // Host module falls back to HostDbSchema (null) then DbSchema ("myapp")
        MultiTenancyDbProperties.DbSchema.ShouldBe("myapp");
    }

    [Fact]
    public void HostModule_BothSet_PrefersHostDbSchema()
    {
        GranitDbDefaults.DbSchema = "myapp";
        GranitDbDefaults.HostDbSchema = "host";

        MultiTenancyDbProperties.DbSchema.ShouldBe("host");
    }

    // ── Tenant module: GranitBlobStorageDbProperties ──────────────────

    [Fact]
    public void TenantModule_NoDefaults_ReturnsNull() =>
        GranitBlobStorageDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void TenantModule_DbSchemaSet_ReturnsDbSchema()
    {
        GranitDbDefaults.DbSchema = "myapp";

        GranitBlobStorageDbProperties.DbSchema.ShouldBe("myapp");
    }

    [Fact]
    public void TenantModule_HostDbSchemaSet_IgnoresIt()
    {
        GranitDbDefaults.HostDbSchema = "host";

        // Tenant modules do NOT fall back to HostDbSchema
        GranitBlobStorageDbProperties.DbSchema.ShouldBeNull();
    }

    [Fact]
    public void TenantModule_BothSet_ReturnsDbSchemaOnly()
    {
        GranitDbDefaults.DbSchema = "myapp";
        GranitDbDefaults.HostDbSchema = "host";

        GranitBlobStorageDbProperties.DbSchema.ShouldBe("myapp");
    }

    // ── Host module: BackgroundJobsDbProperties ──────────────────────

    [Fact]
    public void BackgroundJobs_HostDbSchemaSet_ReturnsHostSchema()
    {
        GranitDbDefaults.HostDbSchema = "infra";

        GranitBackgroundJobsDbProperties.DbSchema.ShouldBe("infra");
    }

    // ── Host module: GranitIdentityDbProperties ──────────────────────

    [Fact]
    public void Identity_HostDbSchemaSet_ReturnsHostSchema()
    {
        GranitDbDefaults.HostDbSchema = "host";

        GranitIdentityDbProperties.DbSchema.ShouldBe("host");
    }

    [Fact]
    public void Identity_OnlyDbSchemaSet_FallsBackToDbSchema()
    {
        GranitDbDefaults.DbSchema = "myapp";

        GranitIdentityDbProperties.DbSchema.ShouldBe("myapp");
    }

    [Fact]
    public void Identity_BothSet_PrefersHostDbSchema()
    {
        GranitDbDefaults.DbSchema = "myapp";
        GranitDbDefaults.HostDbSchema = "host";

        GranitIdentityDbProperties.DbSchema.ShouldBe("host");
    }
}
