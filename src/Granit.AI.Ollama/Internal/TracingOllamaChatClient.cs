using Granit.AI.Diagnostics;
using Granit.AI.Ollama.Diagnostics;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;

namespace Granit.AI.Ollama.Internal;

/// <summary>
/// Decorator that emits OpenTelemetry GenAI spans around the inner Ollama <see cref="IChatClient"/>.
/// Behaviour lives in <see cref="TracingChatClient"/>; this subclass only binds the Ollama
/// activity source and <c>gen_ai.system</c> identifier. <see cref="TracingChatClient.GetService"/>
/// returns the decorator for a matching request, otherwise delegates to the inner client — so
/// callers can still retrieve the underlying <c>OllamaApiClient</c>.
/// </summary>
internal sealed class TracingOllamaChatClient(
    IChatClient inner,
    string requestedModel,
    AIProviderCredential credential)
    : TracingChatClient(inner, requestedModel, credential, AIOllamaActivitySource.TracingProfile);
