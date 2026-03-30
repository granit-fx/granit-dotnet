// =============================================================================
// Tests - DefaultTenantSchemaProvider
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class DefaultTenantSchemaProviderTests
{
    private static readonly Guid TenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    // -----------------------------------------------------------------------
    // Convention TenantId (par défaut)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSchemaNameAsync_TenantIdConvention_ReturnsPrefixedGuid()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = TenantSchemaNamingConvention.TenantId,
                Prefix = "tenant_",
            }));

        string schema = await provider.GetSchemaNameAsync(TenantId, TestContext.Current.CancellationToken);

        // GUID sans tirets, en minuscules.
        schema.ShouldBe("tenant_3fa85f6457174562b3fc2c963f66afa6");
    }

    [Fact]
    public async Task GetSchemaNameAsync_CustomPrefix_AppliesPrefix()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = TenantSchemaNamingConvention.TenantId,
                Prefix = "t_",
            }));

        string schema = await provider.GetSchemaNameAsync(TenantId, TestContext.Current.CancellationToken);

        schema.ShouldStartWith("t_");
    }

    // -----------------------------------------------------------------------
    // Convention TenantName → exception (implémentation custom requise)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSchemaNameAsync_TenantNameConvention_ThrowsInvalidOperationException()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = TenantSchemaNamingConvention.TenantName,
            }));

        Func<Task> act = async () => await provider.GetSchemaNameAsync(TenantId);

        InvalidOperationException ex1 = await Should.ThrowAsync<InvalidOperationException>(act);
        ex1.Message.ShouldContain("custom");
        ex1.Message.ShouldContain("ITenantSchemaProvider");
    }

    // -----------------------------------------------------------------------
    // Convention Custom → exception (implémentation custom requise)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSchemaNameAsync_CustomConvention_ThrowsInvalidOperationException()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = TenantSchemaNamingConvention.Custom,
            }));

        Func<Task> act = async () => await provider.GetSchemaNameAsync(TenantId);

        InvalidOperationException ex2 = await Should.ThrowAsync<InvalidOperationException>(act);
        ex2.Message.ShouldContain("Custom");
        ex2.Message.ShouldContain("ITenantSchemaProvider");
    }
}
