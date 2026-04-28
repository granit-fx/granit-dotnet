using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Granit.Analytics.EntityFrameworkCore.Tests.Integration;

internal static class TestEngineFactory
{
    public static (ServiceProvider Provider, IQueryEngine<TEntity> Engine) Build<TEntity, TDefinition>()
        where TEntity : class
        where TDefinition : QueryDefinition<TEntity>, new()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddOptions<QueryEngineOptions>();
        services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QueryEngineOptions>>().Value);
        services.AddGranitQueryEngine();
        services.AddScoped(typeof(IQueryEngine<>), GetQueryEngineImplementationType());
        services.AddQueryDefinition<TEntity, TDefinition>();

        ServiceProvider provider = services.BuildServiceProvider();
        IQueryEngine<TEntity> engine = provider.GetRequiredService<IQueryEngine<TEntity>>();
        return (provider, engine);
    }

    private static Type GetQueryEngineImplementationType() =>
        typeof(QueryEngine.EntityFrameworkCore.GranitQueryEngineEntityFrameworkCoreModule)
            .Assembly
            .GetType("Granit.QueryEngine.EntityFrameworkCore.Internal.QueryEngine`1", throwOnError: true)!;
}
