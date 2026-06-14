using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Endpoints;
using Granit.AI.Workspaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AI.Endpoints.Tests;

/// <summary>Integration tests for the native-SSE chat streaming endpoint (#496 migration).</summary>
public sealed class AIChatStreamEndpointTests : IAsyncDisposable
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IAIUsageTracker _usageTracker = Substitute.For<IAIUsageTracker>();
    private readonly IAIUsageRecordFactory _usageRecordFactory = Substitute.For<IAIUsageRecordFactory>();
    private readonly WebApplication _app;
    private readonly HttpClient _client;

    public AIChatStreamEndpointTests()
    {
        _workspaceProvider.GetAsync("my-gpt4", Arg.Any<CancellationToken>())
            .Returns(new AIWorkspace { Name = "my-gpt4", Provider = "OpenAI", Model = "gpt-4o" });
        _usageRecordFactory.Create(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan?>())
            .Returns(ci => new AIUsageRecord
            {
                Id = Guid.NewGuid(),
                WorkspaceName = (string)ci[0],
                Provider = (string)ci[1],
                Model = (string)ci[2],
                InputTokens = (int)ci[3],
                OutputTokens = (int)ci[4],
                Timestamp = DateTimeOffset.UnixEpoch,
            });

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(_chatClientFactory);
        builder.Services.AddSingleton(_workspaceProvider);
        builder.Services.AddSingleton(_usageTracker);
        builder.Services.AddSingleton(_usageRecordFactory);
        builder.Services.AddSingleton(Substitute.For<IAIChatCompletionService>());

        _app = builder.Build();
        // Map the internal chat group directly — authorization is covered separately.
        _app.MapGroup("").MapChatEndpoints();
        _app.StartAsync().GetAwaiter().GetResult();
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Stream_emits_delta_then_usage_frames_and_records_usage()
    {
        _chatClientFactory.CreateAsync("my-gpt4", Arg.Any<CancellationToken>())
            .Returns(new FakeChatClient([
                Text("Hello"),
                Text(" world"),
                Usage(5, 2),
            ]));

        IReadOnlyList<AIChatStreamEvent> frames = await StreamAsync("my-gpt4");

        frames.Where(f => f.Type == "delta").Select(f => f.Content).ShouldBe(["Hello", " world"]);
        AIChatStreamEvent usage = frames.Single(f => f.Type == "usage");
        usage.InputTokens.ShouldBe(5);
        usage.OutputTokens.ShouldBe(2);
        frames.ShouldNotContain(f => f.Type == "error");
        await _usageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 5 && r.OutputTokens == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stream_unknown_workspace_returns_404_not_a_stream()
    {
        _workspaceProvider.GetAsync("ghost", Arg.Any<CancellationToken>()).Returns((AIWorkspace?)null);

        HttpResponseMessage response = await PostAsync("ghost");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldNotBe("text/event-stream");
    }

    [Fact]
    public async Task Stream_provider_failure_before_any_content_returns_an_http_problem()
    {
        _chatClientFactory.CreateAsync("my-gpt4", Arg.Any<CancellationToken>())
            .Returns(new FakeChatClient([], throwAfter: 0, exception: new HttpRequestException("boom")));

        HttpResponseMessage response = await PostAsync("my-gpt4");

        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldNotBe("text/event-stream");
    }

    [Fact]
    public async Task Stream_provider_failure_after_content_emits_an_error_frame()
    {
        _chatClientFactory.CreateAsync("my-gpt4", Arg.Any<CancellationToken>())
            .Returns(new FakeChatClient([Text("partial")], throwAfter: 1, exception: new HttpRequestException("boom")));

        IReadOnlyList<AIChatStreamEvent> frames = await StreamAsync("my-gpt4");

        frames.ShouldContain(f => f.Type == "delta" && f.Content == "partial");
        frames.ShouldContain(f => f.Type == "error" && !string.IsNullOrWhiteSpace(f.Error));
        frames.ShouldNotContain(f => f.Type == "usage");
    }

    private Task<HttpResponseMessage> PostAsync(string workspace) =>
        _client.PostAsJsonAsync(
            $"/chat/{workspace}/stream",
            new AIChatRequest([new AIChatMessageRequest("user", "hi")]),
            TestContext.Current.CancellationToken);

    private async Task<IReadOnlyList<AIChatStreamEvent>> StreamAsync(string workspace)
    {
        HttpResponseMessage response = await PostAsync(workspace);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/event-stream");

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        List<AIChatStreamEvent> frames = [];
        foreach (string line in body.Split('\n'))
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                string json = line["data:".Length..].Trim();
                if (json.Length > 0)
                {
                    frames.Add(JsonSerializer.Deserialize<AIChatStreamEvent>(json, JsonSerializerOptions.Web)!);
                }
            }
        }

        return frames;
    }

    private static ChatResponseUpdate Text(string text) => new() { Contents = [new TextContent(text)] };

    private static ChatResponseUpdate Usage(int input, int output) =>
        new() { Contents = [new UsageContent(new UsageDetails { InputTokenCount = input, OutputTokenCount = output })] };

    private sealed class FakeChatClient(
        IReadOnlyList<ChatResponseUpdate> updates, int throwAfter = -1, Exception? exception = null) : IChatClient
    {
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < updates.Count; i++)
            {
                if (i == throwAfter && exception is not null)
                {
                    throw exception;
                }

                yield return updates[i];
                await Task.Yield();
            }

            if (throwAfter == updates.Count && exception is not null)
            {
                throw exception;
            }
        }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
