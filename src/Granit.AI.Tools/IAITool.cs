using System.Text.Json;

namespace Granit.AI.Tools;

/// <summary>
/// The single seam between the chat orchestrator and every capability it can call
/// (ADR-067, "agent-as-tool"). The orchestrator knows <em>only</em> tools; what sits
/// behind a tool is an implementation detail:
/// <list type="bullet">
///   <item>deterministic code (e.g. <c>query_data</c>, <c>search</c>);</item>
///   <item>a single-shot sub-agent — one LLM call to a capability-specific workspace
///         (e.g. <c>translate</c>, <c>extract_text_from_image</c>);</item>
///   <item>a looping sub-agent with its own tools (phase 2).</item>
/// </list>
/// A tool is <em>self-describing</em> (<see cref="Name"/>, <see cref="Description"/>,
/// <see cref="ParameterSchema"/>) so its declaration can be emitted automatically into
/// <c>ChatOptions.Tools</c> with no hand-written tool list.
/// </summary>
/// <remarks>
/// Tools are exposed by explicit <em>application</em> registration (opt-in), never by a
/// framework-wide attribute — each application exposes a different slice of its data.
/// Every tool runs strictly under the calling user's identity and ACLs: an implementation
/// must never read or do anything the caller could not. ACL gating and the invocation
/// loop are layered on top of this seam in later stories.
/// </remarks>
public interface IAITool
{
    /// <summary>
    /// The tool's wire name, surfaced to the model. Must match the function-name
    /// vocabulary accepted by providers: letters, digits, underscore and hyphen only
    /// (<c>snake_case</c> preferred, e.g. <c>query_data</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A concise, model-facing description of what the tool does and when to use it.
    /// This is prompt surface — write it for the model, not for a developer.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The JSON Schema (an <c>object</c> schema) describing the tool's parameters,
    /// emitted verbatim into the tool declaration. Use <see cref="AIToolSchema.Empty"/>
    /// for a parameterless tool.
    /// </summary>
    JsonElement ParameterSchema { get; }

    /// <summary>
    /// Executes the tool for a single model-issued call.
    /// </summary>
    /// <param name="context">The invocation context, carrying the model-supplied arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result fed back to the model on the next turn.</returns>
    ValueTask<AIToolResult> InvokeAsync(
        AIToolInvocationContext context,
        CancellationToken cancellationToken = default);
}
