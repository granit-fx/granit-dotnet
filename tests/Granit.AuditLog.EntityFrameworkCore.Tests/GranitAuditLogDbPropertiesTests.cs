using Shouldly;
using Xunit;

namespace Granit.AuditLog.EntityFrameworkCore.Tests;

public sealed class GranitAuditLogDbPropertiesTests : IDisposable
{
    private readonly string _originalPrefix;
    private readonly string? _originalSchema;

    public GranitAuditLogDbPropertiesTests()
    {
        _originalPrefix = GranitAuditLogDbProperties.DbTablePrefix;
        _originalSchema = GranitAuditLogDbProperties.DbSchema;
    }

    public void Dispose()
    {
        GranitAuditLogDbProperties.DbTablePrefix = _originalPrefix;
        GranitAuditLogDbProperties.DbSchema = _originalSchema;
    }

    [Fact]
    public void DbTablePrefix_Default_IsAuditUnderscore() => GranitAuditLogDbProperties.DbTablePrefix.ShouldBe("audit_");

    [Fact]
    public void DbSchema_Default_IsNull() => GranitAuditLogDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbTablePrefix_CanBeSet()
    {
        GranitAuditLogDbProperties.DbTablePrefix = "custom_";

        GranitAuditLogDbProperties.DbTablePrefix.ShouldBe("custom_");
    }

    [Fact]
    public void DbSchema_CanBeSet()
    {
        GranitAuditLogDbProperties.DbSchema = "audit_schema";

        GranitAuditLogDbProperties.DbSchema.ShouldBe("audit_schema");
    }
}
