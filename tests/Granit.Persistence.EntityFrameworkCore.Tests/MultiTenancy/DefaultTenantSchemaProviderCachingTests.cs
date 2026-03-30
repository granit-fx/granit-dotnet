using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class DefaultTenantSchemaProviderCachingTests
{
    private static readonly Guid TenantId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    [Fact]
    public async Task GetSchemaNameAsync_SameTenantCalledTwice_ReturnsCachedResult()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = TenantSchemaNamingConvention.TenantId,
                Prefix = "tenant_",
            }));

        string schema1 = await provider.GetSchemaNameAsync(TenantId, TestContext.Current.CancellationToken);
        string schema2 = await provider.GetSchemaNameAsync(TenantId, TestContext.Current.CancellationToken);

        schema1.ShouldBe(schema2);
    }

    [Fact]
    public async Task GetSchemaNameAsync_DifferentTenants_ReturnDifferentSchemas()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = TenantSchemaNamingConvention.TenantId,
                Prefix = "tenant_",
            }));

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        string schemaA = await provider.GetSchemaNameAsync(tenantA, TestContext.Current.CancellationToken);
        string schemaB = await provider.GetSchemaNameAsync(tenantB, TestContext.Current.CancellationToken);

        schemaA.ShouldNotBe(schemaB);
    }

    [Fact]
    public async Task GetSchemaNameAsync_EmptyPrefix_ReturnsGuidOnly()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = TenantSchemaNamingConvention.TenantId,
                Prefix = "",
            }));

        string schema = await provider.GetSchemaNameAsync(TenantId, TestContext.Current.CancellationToken);

        schema.ShouldBe(TenantId.ToString("N"));
    }

    [Fact]
    public async Task GetSchemaNameAsync_InvalidConventionValue_ThrowsInvalidOperationException()
    {
        DefaultTenantSchemaProvider provider = new(
            Options.Create(new TenantSchemaOptions
            {
                NamingConvention = (TenantSchemaNamingConvention)999,
                Prefix = "t_",
            }));

        Func<Task> act = async () => await provider.GetSchemaNameAsync(TenantId);

        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
