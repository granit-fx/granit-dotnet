using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.PostGIS.Extensions;
using Granit.Analytics.PostGIS.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.PostGIS.Tests;

/// <summary>
/// Pins the public DI surface — once <see cref="GranitAnalyticsPostGISServiceCollectionExtensions.AddGranitAnalyticsPostGIS"/>
/// is called, <see cref="IGeographyPointProjector{TEntity}"/> resolves to the
/// NTS-backed implementation for any <c>TEntity</c>, scoped lifetime, and
/// the registration is idempotent (TryAdd semantics).
/// </summary>
public sealed class GranitAnalyticsPostGISServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitAnalyticsPostGIS_ResolvesNtsProjectorForAnyEntity()
    {
        ServiceCollection services = new();
        services.AddGranitAnalyticsPostGIS();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        IGeographyPointProjector<TestEntity> projector =
            scope.ServiceProvider.GetRequiredService<IGeographyPointProjector<TestEntity>>();

        projector.ShouldBeOfType<NtsGeographyPointProjector<TestEntity>>();
    }

    [Fact]
    public void AddGranitAnalyticsPostGIS_IsIdempotent_DoesNotDoubleRegister()
    {
        ServiceCollection services = new();
        services.AddGranitAnalyticsPostGIS();
        services.AddGranitAnalyticsPostGIS();

        services.Count(d => d.ServiceType == typeof(IGeographyPointProjector<>)).ShouldBe(1);
    }

    [Fact]
    public void AddGranitAnalyticsPostGIS_DoesNotOverridePreExistingRegistration()
    {
        ServiceCollection services = new();
        services.AddScoped(typeof(IGeographyPointProjector<>), typeof(CustomProjector<>));
        services.AddGranitAnalyticsPostGIS();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // TryAdd semantics: pre-existing registration wins.
        scope.ServiceProvider.GetRequiredService<IGeographyPointProjector<TestEntity>>()
            .ShouldBeOfType<CustomProjector<TestEntity>>();
    }

    private sealed class TestEntity
    {
        public string Code { get; init; } = string.Empty;
    }

    private sealed class CustomProjector<TEntity> : IGeographyPointProjector<TEntity>
        where TEntity : class
    {
        public (double Latitude, double Longitude)? TryProject(TEntity entity, string geographyColumn) => null;
    }
}
