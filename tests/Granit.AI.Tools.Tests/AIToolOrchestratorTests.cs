using Granit.AI.Diagnostics;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Tools.Diagnostics;
using Granit.AI.Tools.Internal;
using Granit.AI.Tools.Options;
using Granit.AI.Tools.Tests.Fakes;
using Granit.AI.Workspaces;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.AI.Tools.Tests;

public sealed class AIToolOrchestratorTests
{
    private static ChatResponse ToolCall(string callId, string name, IDictionary<string, object?>? args = null) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(callId, name, args)]));

    private static ChatResponse FinalText(string text, int? input = null, int? output = null)
    {
        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, text));
        if (input is not null || output is not null)
        {
            response.Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output };
        }

        return response;
    }

    private sealed record Harness(
        AIToolOrchestrator Orchestrator,
        ScriptedChatClient ChatClient,
        IAIUsageTracker UsageTracker);

    private static Harness CreateHarness(
        ScriptedChatClient chatClient,
        IEnumerable<IAITool> tools,
        GranitAIToolsOrchestrationOptions? options = null)
    {
        AIToolRegistry registry = new(tools);
        AIToolProjector projector = new(registry);

        AIWorkspace workspace = new() { Key = "default", Provider = "OpenAI", Model = "gpt-4o" };

        IAIWorkspaceProvider workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
        workspaceProvider.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(workspace);

        // Real usage pipeline: the orchestrator populates the scoped AIUsageContext, the real
        // record factory applies it, and the factory-applied middleware stamps one record per
        // model round-trip — exactly what production wiring does.
        AIUsageContext usageContext = new();
        IAIUsageRecordFactory recordFactory = new AIUsageRecordFactory(
            Substitute.For<ICurrentTenant>(),
            Substitute.For<ICurrentUserService>(),
            usageContext,
            Substitute.For<IGuidGenerator>(),
            TimeProvider.System);

        IAIUsageTracker usageTracker = Substitute.For<IAIUsageTracker>();

        IAIChatClientFactory factory = Substitute.For<IAIChatClientFactory>();
        factory.CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => new UsageTrackingChatClient(
                chatClient,
                "default",
                workspace,
                usageTracker,
                recordFactory,
                new AIMetrics(new TestMeterFactory()),
                TimeProvider.System,
                NullLogger<UsageTrackingChatClient>.Instance));

        IAIToolAuthorizer authorizer = Substitute.For<IAIToolAuthorizer>();
        authorizer.FilterAuthorizedAsync(Arg.Any<IReadOnlyList<IAITool>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<IReadOnlyList<IAITool>>()));

        AIToolOrchestrator orchestrator = new(
            factory,
            workspaceProvider,
            projector,
            registry,
            authorizer,
            new DefaultAISystemPromptComposer(new DefaultAIGuardrailProvider()),
            usageContext,
            MsOptions.Create(options ?? new GranitAIToolsOrchestrationOptions()),
            MsOptions.Create(new GranitAIOptions()),
            new AIToolsMetrics(new TestMeterFactory()),
            TimeProvider.System,
            NullLogger<AIToolOrchestrator>.Instance);

        return new Harness(orchestrator, chatClient, usageTracker);
    }

    private static AIOrchestrationRequest UserSays(string text) =>
        new() { Messages = [new ChatMessage(ChatRole.User, text)] };

    [Fact]
    public async Task Executes_a_tool_call_then_settles_on_the_final_answer()
    {
        FakeAITool echo = new(name: "echo", result: "tool-said-hi");
        ScriptedChatClient client = new(
            ToolCall("c1", "echo"),
            FinalText("final answer"));
        Harness harness = CreateHarness(client, [echo]);

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            UserSays("hi"), TestContext.Current.CancellationToken);

        result.Content.ShouldBe("final answer");
        result.Iterations.ShouldBe(2);
        result.MaxIterationsReached.ShouldBeFalse();
        result.ToolInvocations.ShouldHaveSingleItem();
        result.ToolInvocations[0].ToolName.ShouldBe("echo");
        result.ToolInvocations[0].Succeeded.ShouldBeTrue();
        result.ToolInvocations[0].Iteration.ShouldBe(1);
    }

    [Fact]
    public async Task A_halting_tool_stops_the_loop_and_surfaces_the_interrupt()
    {
        FakeAITool ask = new(name: "ask", result: "asked", interrupt: new AIToolInterrupt("clarification", "PAYLOAD"));
        ScriptedChatClient client = new(
            ToolCall("c1", "ask"),
            FinalText("should never be reached"));
        Harness harness = CreateHarness(client, [ask]);

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            UserSays("ambiguous"), TestContext.Current.CancellationToken);

        result.Interrupt.ShouldNotBeNull();
        result.Interrupt.Kind.ShouldBe("clarification");
        result.Interrupt.Payload.ShouldBe("PAYLOAD");
        result.Content.ShouldBeEmpty();
        result.Iterations.ShouldBe(1);
        // The loop halted: the model was only called once (no feed-back round-trip).
        harness.ChatClient.Calls.Count.ShouldBe(1);
        result.ToolInvocations.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Feeds_the_tool_result_back_into_the_next_model_call()
    {
        FakeAITool echo = new(name: "echo", result: "tool-said-hi");
        ScriptedChatClient client = new(
            ToolCall("c1", "echo"),
            FinalText("done"));
        Harness harness = CreateHarness(client, [echo]);

        await harness.Orchestrator.RunAsync(UserSays("hi"), TestContext.Current.CancellationToken);

        // Second model call must include a Tool message carrying the tool result.
        IReadOnlyList<ChatMessage> secondCall = harness.ChatClient.Calls[1];
        FunctionResultContent resultContent = secondCall
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .ShouldHaveSingleItem();
        resultContent.CallId.ShouldBe("c1");
        resultContent.Result?.ToString().ShouldBe("tool-said-hi");
    }

    [Fact]
    public async Task Stops_at_the_iteration_cap_when_the_model_keeps_calling_tools()
    {
        FakeAITool echo = new(name: "echo");
        ScriptedChatClient client = new(
            ToolCall("c1", "echo"),
            ToolCall("c2", "echo"),
            ToolCall("c3", "echo"));
        Harness harness = CreateHarness(client, [echo],
            new GranitAIToolsOrchestrationOptions { MaxIterations = 2 });

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            UserSays("loop"), TestContext.Current.CancellationToken);

        result.MaxIterationsReached.ShouldBeTrue();
        // MaxIterations caps the tool-handling rounds; the loop makes one final round-trip that still
        // wants tools (which signals the cap) before stopping.
        result.Iterations.ShouldBe(3);
        result.ToolInvocations.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Truncates_an_oversized_tool_result_and_signals_it()
    {
        FakeAITool big = new(name: "big", result: new string('x', 50));
        ScriptedChatClient client = new(
            ToolCall("c1", "big"),
            FinalText("ok"));
        Harness harness = CreateHarness(client, [big],
            new GranitAIToolsOrchestrationOptions { MaxToolResultCharacters = 10 });

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            UserSays("go"), TestContext.Current.CancellationToken);

        result.ToolInvocations[0].Truncated.ShouldBeTrue();

        FunctionResultContent fed = harness.ChatClient.Calls[1]
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        fed.Result.ShouldBeOfType<string>().ShouldContain("[truncated 40 characters");
    }

    [Fact]
    public async Task An_unknown_tool_yields_an_error_result_and_the_loop_continues()
    {
        ScriptedChatClient client = new(
            ToolCall("c1", "ghost"),
            FinalText("recovered"));
        Harness harness = CreateHarness(client, []);

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            UserSays("call ghost"), TestContext.Current.CancellationToken);

        // A call to a tool that was never declared is handled by the function-invoking client itself:
        // it feeds an error result back and the loop continues to a second model round-trip.
        result.Content.ShouldBe("recovered");
        harness.ChatClient.Calls.Count.ShouldBe(2);

        FunctionResultContent fed = harness.ChatClient.Calls[1]
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .ShouldHaveSingleItem();
        fed.CallId.ShouldBe("c1");
    }

    [Fact]
    public async Task Stamps_a_usage_record_with_summed_tokens()
    {
        FakeAITool echo = new(name: "echo");
        ScriptedChatClient client = new(
            FinalText("done", input: 30, output: 12));
        Harness harness = CreateHarness(client, [echo]);

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            UserSays("hi"), TestContext.Current.CancellationToken);

        result.InputTokens.ShouldBe(30);
        result.OutputTokens.ShouldBe(12);
        await harness.UsageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 30 && r.OutputTokens == 12),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_model_round_trip_stamps_its_own_usage_record_sharing_the_conversation()
    {
        // BREAKING semantics locked in by the usage middleware: an agentic turn of N model
        // round-trips produces N records (linked by ConversationId), while the orchestration
        // result still reports the turn totals.
        FakeAITool echo = new(name: "echo", result: "r");
        ChatResponse toolCall = ToolCall("c1", "echo");
        toolCall.Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 2 };
        ScriptedChatClient client = new(
            toolCall,
            FinalText("done", input: 20, output: 3));
        Harness harness = CreateHarness(client, [echo]);

        var conversationId = Guid.NewGuid();
        AIOrchestrationRequest request = UserSays("hi") with { ConversationId = conversationId };

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            request, TestContext.Current.CancellationToken);

        result.InputTokens.ShouldBe(30);
        result.OutputTokens.ShouldBe(5);
        await harness.UsageTracker.Received(2).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.ConversationId == conversationId),
            Arg.Any<CancellationToken>());
        await harness.UsageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 10 && r.OutputTokens == 2),
            Arg.Any<CancellationToken>());
        await harness.UsageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 20 && r.OutputTokens == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Prepends_a_system_message_carrying_the_framework_guardrails()
    {
        ScriptedChatClient client = new(FinalText("hi"));
        Harness harness = CreateHarness(client, []);

        await harness.Orchestrator.RunAsync(UserSays("hello"), TestContext.Current.CancellationToken);

        ChatMessage first = harness.ChatClient.Calls[0][0];
        first.Role.ShouldBe(ChatRole.System);
        first.Text.ShouldContain("Stay within the calling user's authorization");
    }

    [Fact]
    public async Task Stamps_the_guardrail_version_into_the_usage_record()
    {
        ScriptedChatClient client = new(FinalText("done", input: 5, output: 2));
        Harness harness = CreateHarness(client, []);

        await harness.Orchestrator.RunAsync(UserSays("hi"), TestContext.Current.CancellationToken);

        await harness.UsageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.PromptVersion == "1.2.0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stamps_the_invoked_catalogue_prompt_into_the_usage_record()
    {
        ScriptedChatClient client = new(FinalText("done", input: 5, output: 2));
        Harness harness = CreateHarness(client, []);

        AIOrchestrationRequest request = UserSays("hi") with
        {
            InvokedPromptName = "Prompt:Summarize:Name",
            InvokedPromptVersion = 4,
        };

        await harness.Orchestrator.RunAsync(request, TestContext.Current.CancellationToken);

        await harness.UsageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.PromptTemplateName == "Prompt:Summarize:Name" && r.PromptTemplateVersion == 4),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stamps_the_conversation_into_the_usage_record()
    {
        ScriptedChatClient client = new(FinalText("done", input: 5, output: 2));
        Harness harness = CreateHarness(client, []);

        var conversationId = Guid.NewGuid();
        AIOrchestrationRequest request = UserSays("hi") with { ConversationId = conversationId };

        await harness.Orchestrator.RunAsync(request, TestContext.Current.CancellationToken);

        await harness.UsageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.ConversationId == conversationId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leaves_the_conversation_null_when_the_request_carries_none()
    {
        ScriptedChatClient client = new(FinalText("done", input: 5, output: 2));
        Harness harness = CreateHarness(client, []);

        await harness.Orchestrator.RunAsync(UserSays("hi"), TestContext.Current.CancellationToken);

        await harness.UsageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.ConversationId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_not_stamp_usage_when_the_provider_reports_none()
    {
        ScriptedChatClient client = new(FinalText("done"));
        Harness harness = CreateHarness(client, []);

        AIOrchestrationResult result = await harness.Orchestrator.RunAsync(
            UserSays("hi"), TestContext.Current.CancellationToken);

        result.InputTokens.ShouldBeNull();
        await harness.UsageTracker.DidNotReceive().RecordAsync(
            Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunStreamingAsync_streams_text_deltas_then_a_completion()
    {
        ScriptedChatClient client = new(FinalText("Hello world"));
        Harness harness = CreateHarness(client, []);

        List<AIOrchestrationUpdate> updates = [];
        await foreach (AIOrchestrationUpdate update in harness.Orchestrator
            .RunStreamingAsync(UserSays("hi"), TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        updates[^1].Kind.ShouldBe(AIOrchestrationUpdateKind.Completed);
        updates[^1].Result!.Content.ShouldBe("Hello world");

        string streamed = string.Concat(updates
            .Where(u => u.Kind == AIOrchestrationUpdateKind.Delta)
            .Select(u => u.TextDelta));
        streamed.ShouldBe("Hello world");
    }

    [Fact]
    public async Task Strips_a_reasoning_think_block_from_the_streamed_text_and_the_settled_content()
    {
        // DeepSeek-R1 over Ollama emits its chain-of-thought inline as a leading <think> block; it must
        // never reach the client or the persisted turn.
        ScriptedChatClient client = new(FinalText("<think>weighing options</think>\n\nThe answer is 42."));
        Harness harness = CreateHarness(client, []);

        List<AIOrchestrationUpdate> updates = [];
        await foreach (AIOrchestrationUpdate update in harness.Orchestrator
            .RunStreamingAsync(UserSays("hi"), TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        string streamed = string.Concat(updates
            .Where(u => u.Kind == AIOrchestrationUpdateKind.Delta)
            .Select(u => u.TextDelta));
        streamed.ShouldBe("The answer is 42.");
        updates[^1].Result!.Content.ShouldBe("The answer is 42.");
    }

    [Fact]
    public async Task RunStreamingAsync_emits_tool_call_then_tool_result_once_each()
    {
        FakeAITool echo = new(name: "echo", result: "r");
        ScriptedChatClient client = new(ToolCall("c1", "echo"), FinalText("done"));
        Harness harness = CreateHarness(client, [echo]);

        List<AIOrchestrationUpdate> updates = [];
        await foreach (AIOrchestrationUpdate update in harness.Orchestrator
            .RunStreamingAsync(UserSays("hi"), TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        AIOrchestrationUpdate call = updates.Single(u => u.Kind == AIOrchestrationUpdateKind.ToolCall);
        AIOrchestrationUpdate toolResult = updates.Single(u => u.Kind == AIOrchestrationUpdateKind.ToolResult);
        call.ToolName.ShouldBe("echo");
        call.ToolCallId.ShouldBe("c1");
        toolResult.ToolCallId.ShouldBe("c1");
        toolResult.Succeeded.ShouldBe(true);
        updates.IndexOf(call).ShouldBeLessThan(updates.IndexOf(toolResult));
    }

    [Fact]
    public async Task RunStreamingAsync_propagates_cancellation()
    {
        ScriptedChatClient client = new(FinalText("never"));
        Harness harness = CreateHarness(client, []);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await foreach (AIOrchestrationUpdate _ in harness.Orchestrator.RunStreamingAsync(UserSays("hi"), cts.Token))
            {
            }
        });
    }
}
