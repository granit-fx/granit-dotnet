using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Queries;
using Granit.QueryEngine;
using Granit.QueryEngine.EntityFrameworkCore;
using Granit.QueryEngine.Extensions;
using Granit.QueryEngine.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Chat.EntityFrameworkCore.Tests;

/// <summary>
/// Builds the real <see cref="IQueryEngine{TEntity}"/> for tests. The implementation is internal to
/// its assembly, so the open generic the EF Core module would register is wired by reflection — this
/// keeps the test-only dependency without a contrived <c>InternalsVisibleTo</c>.
/// </summary>
internal static class TestQueryEngine
{
    private static readonly ServiceProvider Provider = Build();

    public static IQueryEngine<Message> ForMessages() => Provider.GetRequiredService<IQueryEngine<Message>>();

    private static ServiceProvider Build()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddOptions<QueryEngineOptions>();
        services.AddQueryDefinition<Message, ChatMessageQueryDefinition>();

        Type engineOpenGeneric = typeof(GranitQueryEngineEntityFrameworkCoreModule).Assembly
            .GetType("Granit.QueryEngine.EntityFrameworkCore.Internal.QueryEngine`1", throwOnError: true)!;
        services.AddSingleton(typeof(IQueryEngine<>), engineOpenGeneric);

        return services.BuildServiceProvider();
    }
}
