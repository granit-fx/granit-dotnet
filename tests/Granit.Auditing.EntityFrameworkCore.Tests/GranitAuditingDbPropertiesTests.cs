using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests;

public sealed class GranitAuditingDbPropertiesTests : IDisposable
{
    private readonly string _originalPrefix;
    private readonly string? _originalSchema;

    public GranitAuditingDbPropertiesTests()
    {
        _originalPrefix = GranitAuditingDbProperties.DbTablePrefix;
        _originalSchema = GranitAuditingDbProperties.DbSchema;
    }

    public void Dispose()
    {
        GranitAuditingDbProperties.DbTablePrefix = _originalPrefix;
        GranitAuditingDbProperties.DbSchema = _originalSchema;
    }

    [Fact]
    public void DbTablePrefix_Default_IsAuditingUnderscore() => GranitAuditingDbProperties.DbTablePrefix.ShouldBe("audit_log_");

    [Fact]
    public void DbSchema_Default_IsNull() => GranitAuditingDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbTablePrefix_CanBeSet()
    {
        GranitAuditingDbProperties.DbTablePrefix = "custom_";

        GranitAuditingDbProperties.DbTablePrefix.ShouldBe("custom_");
    }

    [Fact]
    public void DbSchema_CanBeSet()
    {
        GranitAuditingDbProperties.DbSchema = "audit_schema";

        GranitAuditingDbProperties.DbSchema.ShouldBe("audit_schema");
    }
}
