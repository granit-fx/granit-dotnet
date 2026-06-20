using System.Net;
using System.Net.Http.Json;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Extensions;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Mentions;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Granit.AI.Chat.Endpoints.Tests;

/// <summary>HTTP-level tests for the <c>@</c> mention picker endpoint (<c>GET /conversations/mentions</c>).</summary>
public sealed class MentionsEndpointsHttpTests
{
    private readonly StubSearch _search = new();

    private Task<GranitEndpointTestHost> StartAsync() =>
        GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder()
                    .AddPolicy(AIChatPermissions.Conversations.Send, p =>
                        p.RequireClaim(TestAuthHandler.PermissionClaimType, AIChatPermissions.Conversations.Send));
                services.AddSingleton<IAIMentionSearchService>(_search);
            },
            configureEndpoints: app => app.MapGranitConversations());

    [Fact]
    public async Task Search_without_the_send_permission_is_forbidden()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        HttpClient client = host.CreateClientWithPermissions(AIChatPermissions.Conversations.Read);
        HttpResponseMessage response = await client.GetAsync("/conversations/mentions?q=a", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Search_returns_the_suggestions_for_the_query()
    {
        _search.Result =
        [
            new AIMentionSuggestion { Type = "user", Id = "u1", Label = "Ada Lovelace", Description = "ada@x.io" },
        ];
        await using GranitEndpointTestHost host = await StartAsync();

        MentionSearchResponse? body = await host.CreateClientWithPermissions(AIChatPermissions.Conversations.Send)
            .GetFromJsonAsync<MentionSearchResponse>("/conversations/mentions?q=ada", TestContext.Current.CancellationToken);

        body.ShouldNotBeNull();
        body.Items.ShouldHaveSingleItem();
        body.Items[0].Type.ShouldBe("user");
        body.Items[0].Label.ShouldBe("Ada Lovelace");
        body.Items[0].Description.ShouldBe("ada@x.io");
        _search.LastQuery.ShouldBe("ada");
    }

    [Fact]
    public async Task Search_passes_the_type_filter_through()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        await host.CreateClientWithPermissions(AIChatPermissions.Conversations.Send)
            .GetAsync("/conversations/mentions?q=a&type=user", TestContext.Current.CancellationToken);

        _search.LastType.ShouldBe("user");
    }

    [Fact]
    public async Task Search_clamps_an_oversized_limit_to_the_ceiling()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        await host.CreateClientWithPermissions(AIChatPermissions.Conversations.Send)
            .GetAsync("/conversations/mentions?q=a&limit=9999", TestContext.Current.CancellationToken);

        _search.LastLimit.ShouldBe(25);
    }

    [Fact]
    public async Task Search_defaults_the_limit_when_omitted()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        await host.CreateClientWithPermissions(AIChatPermissions.Conversations.Send)
            .GetAsync("/conversations/mentions?q=a", TestContext.Current.CancellationToken);

        _search.LastLimit.ShouldBe(8);
    }

    [Fact]
    public async Task Search_treats_a_missing_query_as_empty()
    {
        await using GranitEndpointTestHost host = await StartAsync();

        await host.CreateClientWithPermissions(AIChatPermissions.Conversations.Send)
            .GetAsync("/conversations/mentions", TestContext.Current.CancellationToken);

        _search.LastQuery.ShouldBe(string.Empty);
    }

    private sealed class StubSearch : IAIMentionSearchService
    {
        public string? LastQuery { get; private set; }
        public string? LastType { get; private set; }
        public int LastLimit { get; private set; }
        public IReadOnlyList<AIMentionSuggestion> Result { get; set; } = [];

        public ValueTask<IReadOnlyList<AIMentionSuggestion>> SearchAsync(
            string query, string? type = null, int limit = 8, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            LastType = type;
            LastLimit = limit;
            return new(Result);
        }
    }
}
