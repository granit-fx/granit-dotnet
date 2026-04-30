using System.Linq.Expressions;
using Granit.Analytics.EntityFrameworkCore.Extensions;
using Granit.Analytics.Extensions;
using Granit.Analytics.Metrics;
using Granit.QueryEngine;
using Granit.QueryEngine.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Pins the DI lifetime of <c>I*Runner</c> registrations emitted by
/// <c>AddGranitAnalyticsRunners</c>. Every runner transitively depends on
/// <c>IQueryableSource&lt;TEntity&gt;</c> (DbContext-bound) and on the per-request
/// <c>ICurrentTenant</c>, so a Singleton lifetime would freeze the first
/// request's tenant context and leak data across tenants on every subsequent
/// call. This test fails the build the moment a runner is registered as
/// Singleton again.
/// </summary>
public sealed class AnalyticsRunnerLifetimeTests
{
    [Fact]
    public void AddGranitAnalyticsRunners_RegistersAllRunnerInterfacesAsScoped()
    {
        ServiceCollection services = [];
        // One descriptor of each kind so the foreach loops in
        // AddGranitAnalyticsRunners produce at least one registration.
        services.AddMetricDefinition<TestEntity, int, TestMetricDefinition>();
        services.AddSingleton<QueryDefinition<TestEntity>, TestQueryDefinition>();
        services.AddSingleton<IQueryDefinitionDescriptor>(
            sp => sp.GetRequiredService<QueryDefinition<TestEntity>>());

        services.AddGranitAnalyticsRunners();

        // The runner interface names emitted by AddGranitAnalyticsRunners.
        // Every entry must be Scoped — never Singleton, never Transient
        // (Transient would also re-resolve scoped deps from root).
        string[] runnerInterfaceNames =
        [
            "IMetricRunner",
            "IQueryAggregateRunner",
            "ITableRunner",
            "IChartRunner",
            "IPivotRunner",
            "IMapRunner",
        ];

        foreach (string ifaceName in runnerInterfaceNames)
        {
            ServiceDescriptor[] matches = [.. services.Where(d =>
                d.ServiceType.Name == ifaceName)];

            matches.ShouldNotBeEmpty(
                $"AddGranitAnalyticsRunners should register at least one '{ifaceName}'.");

            foreach (ServiceDescriptor descriptor in matches)
            {
                descriptor.Lifetime.ShouldBe(
                    ServiceLifetime.Scoped,
                    $"'{ifaceName}' must be Scoped — Singleton/Transient captures the request's DbContext + ICurrentTenant " +
                    $"and leaks tenant data across requests under .NET's production default (ValidateScopes=false). " +
                    $"Current lifetime: {descriptor.Lifetime}.");
            }
        }
    }

    private sealed class TestEntity
    {
        public int Id { get; set; }
    }

    private sealed class TestMetricDefinition : MetricDefinition<TestEntity, int>
    {
        public override string Name => "Test.Metric";
        public override MetricValueKind ValueKind => MetricValueKind.Count;
        public override AggregateFunction Aggregation => AggregateFunction.Count;
        public override Expression<Func<TestEntity, int?>>? Selector => null;
    }

    private sealed class TestQueryDefinition : QueryDefinition<TestEntity>
    {
        public override string Name => "Test.Entities";

        protected override void Configure(QueryDefinitionBuilder<TestEntity> builder) =>
            builder.Column(x => x.Id, c => c.Label("Id"));
    }
}
