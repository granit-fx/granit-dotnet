using Granit.AI.Tools.Internal;
using Granit.AI.Tools.Prompts;
using Shouldly;

namespace Granit.AI.Tools.Tests;

public sealed class DefaultAISystemPromptComposerTests
{
    private static DefaultAISystemPromptComposer CreateComposer() =>
        new(new DefaultAIGuardrailProvider());

    /// <summary>A tool that also contributes per-tool instructions.</summary>
    private sealed class InstructedTool(string name, string instructions)
        : IAITool, IAIToolInstructions
    {
        public string Name => name;
        public string Description => "desc";
        public System.Text.Json.JsonElement ParameterSchema => AIToolSchema.Empty;
        public string Instructions => instructions;

        public ValueTask<AIToolResult> InvokeAsync(
            AIToolInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(AIToolResult.Success("x"));
    }

    [Fact]
    public void Layers_guardrails_then_workspace_then_user_context_then_tool_instructions()
    {
        DefaultAISystemPromptComposer composer = CreateComposer();

        AISystemPrompt prompt = composer.Compose(new AISystemPromptContext
        {
            WorkspaceSystemPrompt = "WORKSPACE-PROMPT",
            UserCustomContext = "USER-CONTEXT",
            Tools = [new InstructedTool("echo", "TOOL-GUIDANCE")],
        });

        int guardrails = prompt.Text.IndexOf("Stay within the calling user's authorization", StringComparison.Ordinal);
        int workspace = prompt.Text.IndexOf("WORKSPACE-PROMPT", StringComparison.Ordinal);
        int user = prompt.Text.IndexOf("USER-CONTEXT", StringComparison.Ordinal);
        int tool = prompt.Text.IndexOf("TOOL-GUIDANCE", StringComparison.Ordinal);

        guardrails.ShouldBeGreaterThanOrEqualTo(0);
        guardrails.ShouldBeLessThan(workspace);
        workspace.ShouldBeLessThan(user);
        user.ShouldBeLessThan(tool);
    }

    [Fact]
    public void Skips_absent_sections()
    {
        DefaultAISystemPromptComposer composer = CreateComposer();

        AISystemPrompt prompt = composer.Compose(new AISystemPromptContext());

        prompt.Text.ShouldBe(new DefaultAIGuardrailProvider().Guardrails.Content);
    }

    [Fact]
    public void Omits_tools_without_instructions()
    {
        DefaultAISystemPromptComposer composer = CreateComposer();

        AISystemPrompt prompt = composer.Compose(new AISystemPromptContext
        {
            Tools = [new FakeAITool(name: "plain")],
        });

        prompt.Text.ShouldBe(new DefaultAIGuardrailProvider().Guardrails.Content);
    }

    [Fact]
    public void Exposes_the_guardrail_version_for_stamping()
    {
        DefaultAISystemPromptComposer composer = CreateComposer();

        AISystemPrompt prompt = composer.Compose(new AISystemPromptContext());

        prompt.Guardrails.Name.ShouldBe("framework.guardrails");
        prompt.Guardrails.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void Guardrails_have_no_mutation_surface()
    {
        // The guardrail provider exposes only a getter — there is no API (here or tenant-facing)
        // to replace the content. This guards the "non-editable" contract at the type level.
        System.Reflection.PropertyInfo property =
            typeof(IAIGuardrailProvider).GetProperty(nameof(IAIGuardrailProvider.Guardrails))!;

        property.CanRead.ShouldBeTrue();
        property.CanWrite.ShouldBeFalse();
    }
}
