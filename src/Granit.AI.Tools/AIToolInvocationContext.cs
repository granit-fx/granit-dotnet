using System.Text.Json;

namespace Granit.AI.Tools;

/// <summary>
/// Input passed to <see cref="IAITool.InvokeAsync"/> for a single model-issued tool call.
/// </summary>
/// <remarks>
/// The type is deliberately a record so later stories can enrich the invocation surface
/// (e.g. the resolved caller for ACL enforcement) without breaking the seam. The caller's
/// identity and ACLs are ambient on the request scope — a tool resolves its own scoped
/// dependencies; the context only carries call-specific data.
/// </remarks>
public sealed record AIToolInvocationContext
{
    /// <summary>
    /// The model-supplied arguments as a JSON <c>object</c>, shaped by the tool's
    /// <see cref="IAITool.ParameterSchema"/>. For a parameterless tool this is an
    /// empty object.
    /// </summary>
    public required JsonElement Arguments { get; init; }
}
