using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class DualScopeValidationTests
{
    [Theory]
    [InlineData(TenantIsolationStrategy.SharedDatabase)]
    [InlineData(TenantIsolationStrategy.SchemaPerTenant)]
    [InlineData(TenantIsolationStrategy.DatabasePerTenant)]
    public void ValidateStorageMode_Shared_AlwaysSucceeds(TenantIsolationStrategy strategy)
    {
        Should.NotThrow(() => DualScopeValidation.ValidateStorageMode(
            DualScopeStorageMode.Shared,
            strategy,
            "Webhooks"));
    }

    [Theory]
    [InlineData(TenantIsolationStrategy.SchemaPerTenant)]
    [InlineData(TenantIsolationStrategy.DatabasePerTenant)]
    public void ValidateStorageMode_Segregated_AcceptsPhysicalIsolationStrategies(TenantIsolationStrategy strategy)
    {
        Should.NotThrow(() => DualScopeValidation.ValidateStorageMode(
            DualScopeStorageMode.Segregated,
            strategy,
            "Webhooks"));
    }

    [Fact]
    public void ValidateStorageMode_Segregated_RejectsSharedDatabase()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => DualScopeValidation.ValidateStorageMode(
                DualScopeStorageMode.Segregated,
                TenantIsolationStrategy.SharedDatabase,
                "Webhooks"));

        ex.Message.ShouldContain("Webhooks");
        ex.Message.ShouldContain("Segregated");
        ex.Message.ShouldContain("SharedDatabase");
        ex.Message.ShouldContain("ADR-063");
    }

    [Fact]
    public void ValidateStorageMode_NullModuleName_Throws()
    {
        Should.Throw<ArgumentException>(() => DualScopeValidation.ValidateStorageMode(
            DualScopeStorageMode.Shared,
            TenantIsolationStrategy.SharedDatabase,
            moduleName: null!));
    }

    [Fact]
    public void ValidateStorageMode_EmptyModuleName_Throws()
    {
        Should.Throw<ArgumentException>(() => DualScopeValidation.ValidateStorageMode(
            DualScopeStorageMode.Shared,
            TenantIsolationStrategy.SharedDatabase,
            moduleName: string.Empty));
    }
}
