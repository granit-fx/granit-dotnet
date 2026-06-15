using System.Text.Json;
using Granit.AI.Chat.Clarification;
using Granit.AI.Chat.Internal;
using Granit.AI.Tools;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class RequestClarificationToolTests
{
    private static AIToolInvocationContext Args(string json) =>
        new() { Arguments = JsonDocument.Parse(json).RootElement.Clone() };

    private static async Task<AIToolResult> InvokeAsync(string json) =>
        await new RequestClarificationTool().InvokeAsync(Args(json), TestContext.Current.CancellationToken);

    [Fact]
    public void Tool_is_named_request_clarification() =>
        new RequestClarificationTool().Name.ShouldBe("request_clarification");

    [Fact]
    public async Task Valid_call_halts_the_loop_with_a_clarification_payload()
    {
        AIToolResult result = await InvokeAsync(
            """{"question":"Which environment?","options":[{"label":"Prod","value":"prod"},{"label":"Test"}],"allow_other":true}""");

        result.Interrupt.ShouldNotBeNull();
        result.Interrupt.Kind.ShouldBe(AIClarificationRequest.InterruptKind);

        AIClarificationRequest? clarification =
            JsonSerializer.Deserialize<AIClarificationRequest>(result.Interrupt.Payload, JsonSerializerOptions.Web);
        clarification.ShouldNotBeNull();
        clarification.Question.ShouldBe("Which environment?");
        clarification.AllowOther.ShouldBeTrue();
        clarification.Options.Count.ShouldBe(2);
        clarification.Options[0].Label.ShouldBe("Prod");
        clarification.Options[0].Value.ShouldBe("prod");
        clarification.Options[1].Value.ShouldBeNull();
    }

    [Fact]
    public async Task Missing_question_is_an_error_not_an_interrupt()
    {
        AIToolResult result = await InvokeAsync("""{"options":[{"label":"A"}]}""");

        result.IsError.ShouldBeTrue();
        result.Interrupt.ShouldBeNull();
    }

    [Fact]
    public async Task No_options_is_an_error_not_an_interrupt()
    {
        AIToolResult result = await InvokeAsync("""{"question":"Pick one","options":[]}""");

        result.IsError.ShouldBeTrue();
        result.Interrupt.ShouldBeNull();
    }
}
