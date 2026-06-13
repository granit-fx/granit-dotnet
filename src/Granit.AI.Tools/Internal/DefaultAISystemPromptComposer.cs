using System.Text;
using Granit.AI.Tools.Prompts;

namespace Granit.AI.Tools.Internal;

/// <summary>
/// Default <see cref="IAISystemPromptComposer"/>. Layers guardrails → workspace prompt → user
/// custom context → per-tool instructions, separating present sections with a blank line and
/// skipping empty ones. The guardrails always come first so nothing below can override them.
/// </summary>
internal sealed class DefaultAISystemPromptComposer(IAIGuardrailProvider guardrailProvider)
    : IAISystemPromptComposer
{
    public AISystemPrompt Compose(AISystemPromptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        AIPromptVersion guardrails = guardrailProvider.Guardrails;
        StringBuilder builder = new();
        Append(builder, guardrails.Content);
        Append(builder, context.WorkspaceSystemPrompt);
        Append(builder, context.UserCustomContext);
        Append(builder, ComposeToolInstructions(context.Tools));

        return new AISystemPrompt
        {
            Text = builder.ToString(),
            Guardrails = guardrails,
        };
    }

    private static string? ComposeToolInstructions(IReadOnlyList<IAITool>? tools)
    {
        if (tools is null || tools.Count == 0)
        {
            return null;
        }

        StringBuilder builder = new();
        foreach (IAITool tool in tools)
        {
            if (tool is IAIToolInstructions { Instructions: { Length: > 0 } instructions })
            {
                if (builder.Length > 0)
                {
                    builder.Append("\n\n");
                }

                builder.Append("Tool '").Append(tool.Name).Append("': ").Append(instructions);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static void Append(StringBuilder builder, string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append("\n\n");
        }

        builder.Append(section.Trim());
    }
}
