using System.Text.Json;
using Granit.AI.Tools.Extensions;
using Granit.AI.Tools.Search.Extensions;
using Granit.AI.Tools.Search.Internal;
using Granit.AI.Tools.Search.Options;
using Granit.AI.VectorData;
using Granit.Indexing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tools.Search.Tests;

public sealed class SearchToolTests
{
    public sealed record ArticleHit(Guid Id, string Snippet);

    private static AIToolInvocationContext Args(object value) =>
        new() { Arguments = JsonSerializer.SerializeToElement(value) };

    private static SearchTool SemanticTool(ISemanticSearchService svc, GranitAIToolsSearchOptions? options = null) =>
        new(new SemanticCorpusSearcher("docs", "Product docs", "documentation", svc),
            options ?? new GranitAIToolsSearchOptions());

    [Fact]
    public void Tool_name_is_prefixed_with_search()
    {
        SearchTool tool = SemanticTool(Substitute.For<ISemanticSearchService>());
        tool.Name.ShouldBe("search_docs");
    }

    [Fact]
    public void Schema_requires_a_query_and_caps_the_limit()
    {
        SearchTool tool = SemanticTool(Substitute.For<ISemanticSearchService>(),
            new GranitAIToolsSearchOptions { MaxLimit = 12 });

        JsonElement props = tool.ParameterSchema.GetProperty("properties");
        props.GetProperty("query").GetProperty("type").GetString().ShouldBe("string");
        props.GetProperty("limit").GetProperty("maximum").GetInt32().ShouldBe(12);
        tool.ParameterSchema.GetProperty("required")[0].GetString().ShouldBe("query");
    }

    [Fact]
    public async Task Missing_query_returns_an_error_without_searching()
    {
        ISemanticSearchService svc = Substitute.For<ISemanticSearchService>();
        SearchTool tool = SemanticTool(svc);

        AIToolResult result = await tool.InvokeAsync(Args(new { }), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeTrue();
        await svc.DidNotReceive().SearchAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Semantic_search_returns_scored_snippets()
    {
        ISemanticSearchService svc = Substitute.For<ISemanticSearchService>();
        svc.SearchAsync("documentation", "how to deploy", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new("doc-1", 0.91, "Deploy with the CLI.")]);
        SearchTool tool = SemanticTool(svc);

        AIToolResult result = await tool.InvokeAsync(
            Args(new { query = "how to deploy" }), TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(result.Content);
        JsonElement root = payload.RootElement;
        root.GetProperty("corpus").GetString().ShouldBe("docs");
        root.GetProperty("mode").GetString().ShouldBe("semantic");
        JsonElement snippet = root.GetProperty("snippets")[0];
        snippet.GetProperty("id").GetString().ShouldBe("doc-1");
        snippet.GetProperty("text").GetString().ShouldBe("Deploy with the CLI.");
        snippet.GetProperty("score").GetDouble().ShouldBe(0.91);
        snippet.GetProperty("source").GetString().ShouldBe("semantic");
    }

    [Fact]
    public async Task Limit_is_clamped_to_the_configured_maximum()
    {
        ISemanticSearchService svc = Substitute.For<ISemanticSearchService>();
        svc.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        SearchTool tool = SemanticTool(svc, new GranitAIToolsSearchOptions { MaxLimit = 10 });

        await tool.InvokeAsync(Args(new { query = "x", limit = 9999 }), TestContext.Current.CancellationToken);

        await svc.Received(1).SearchAsync("documentation", "x", 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Full_text_search_returns_unscored_snippets_with_source_ids()
    {
        ISearchService<Guid, ArticleHit> svc = Substitute.For<ISearchService<Guid, ArticleHit>>();
        Guid id = Guid.Empty;
        svc.SearchAsync(Arg.Any<SearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchPage<ArticleHit>
            {
                Items = [new ArticleHit(id, "Full text passage.")],
                Page = 1,
                PageSize = 5,
                TotalAuthorized = 1,
            });

        SearchTool tool = new(
            new FullTextCorpusSearcher<Guid, ArticleHit>(
                "articles", null, hit => hit.Snippet, hit => hit.Id.ToString(), svc),
            new GranitAIToolsSearchOptions());

        AIToolResult result = await tool.InvokeAsync(
            Args(new { query = "passage" }), TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(result.Content);
        JsonElement snippet = payload.RootElement.GetProperty("snippets")[0];
        snippet.GetProperty("source").GetString().ShouldBe("full_text");
        snippet.GetProperty("text").GetString().ShouldBe("Full text passage.");
        snippet.GetProperty("id").GetString().ShouldBe(id.ToString());
        snippet.TryGetProperty("score", out _).ShouldBeFalse(); // null score omitted
    }

    [Fact]
    public void Only_opted_in_corpora_are_exposed_as_tools()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(Substitute.For<ISemanticSearchService>());
        services.AddGranitAITools(tools => tools.AddSearch(s => s.AddSemantic("docs", "documentation")));

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAIToolRegistry registry = scope.ServiceProvider.GetRequiredService<IAIToolRegistry>();

        registry.TryGet("search_docs", out _).ShouldBeTrue();
        registry.TryGet("search_articles", out _).ShouldBeFalse();
        registry.Tools.ShouldHaveSingleItem();
    }
}
