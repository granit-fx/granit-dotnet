using System.Diagnostics;

namespace Granit.AI.Diagnostics;

/// <summary>
/// Identifies the OpenTelemetry <see cref="ActivitySource"/> and GenAI semantic-convention
/// values a provider supplies to <see cref="TracingChatClient"/>. Bundling them lets every
/// provider reuse the same tracing decorator while keeping the span names and
/// <c>gen_ai.system</c> identifier owned by the provider package.
/// </summary>
/// <param name="Source">The provider's activity source.</param>
/// <param name="SystemTagValue">OTel <c>gen_ai.system</c> identifier (e.g. <c>openai</c>).</param>
/// <param name="ChatOperation">Span name for a non-streaming chat completion.</param>
/// <param name="ChatStreamOperation">Span name for a streaming chat completion.</param>
public sealed record AIChatTracingProfile(
    ActivitySource Source,
    string SystemTagValue,
    string ChatOperation,
    string ChatStreamOperation);
