// =============================================================================
// Tests - ConfigurationTenantIsolationStrategyProvider
// =============================================================================
// Vérifie la lecture de la stratégie depuis IOptions<TenantIsolationOptions>
// et le comportement par défaut (SharedDatabase) en l'absence de configuration.
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.MultiTenancy;

public sealed class ConfigurationTenantIsolationStrategyProviderTests
{
    private static ConfigurationTenantIsolationStrategyProvider Build(
        TenantIsolationStrategy strategy) =>
        new(Options.Create(new TenantIsolationOptions { Strategy = strategy }));

    // -----------------------------------------------------------------------
    // Valeur par défaut
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetStrategyAsync_DefaultOptions_ReturnsSharedDatabase()
    {
        ConfigurationTenantIsolationStrategyProvider provider = new(
            Options.Create(new TenantIsolationOptions()));

        TenantIsolationStrategy strategy = await provider.GetStrategyAsync(
            null, TestContext.Current.CancellationToken);

        strategy.ShouldBe(TenantIsolationStrategy.SharedDatabase);
    }

    // -----------------------------------------------------------------------
    // Chaque valeur configurée est retournée quelle que soit la valeur tenantId
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(TenantIsolationStrategy.SharedDatabase)]
    [InlineData(TenantIsolationStrategy.DatabasePerTenant)]
    [InlineData(TenantIsolationStrategy.SchemaPerTenant)]
    public async Task GetStrategyAsync_ConfiguredStrategy_ReturnsConfiguredValue(
        TenantIsolationStrategy configured)
    {
        ConfigurationTenantIsolationStrategyProvider provider = Build(configured);

        TenantIsolationStrategy strategy = await provider.GetStrategyAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        strategy.ShouldBe(configured);
    }

    // -----------------------------------------------------------------------
    // tenantId null → même stratégie retournée (provider statique)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(TenantIsolationStrategy.DatabasePerTenant)]
    [InlineData(TenantIsolationStrategy.SchemaPerTenant)]
    public async Task GetStrategyAsync_NullTenantId_ReturnsConfiguredStrategy(
        TenantIsolationStrategy configured)
    {
        ConfigurationTenantIsolationStrategyProvider provider = Build(configured);

        TenantIsolationStrategy strategy = await provider.GetStrategyAsync(
            null, TestContext.Current.CancellationToken);

        strategy.ShouldBe(configured);
    }
}
