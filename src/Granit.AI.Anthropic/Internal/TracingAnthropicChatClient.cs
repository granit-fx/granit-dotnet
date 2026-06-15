using Granit.AI.Anthropic.Diagnostics;
using Granit.AI.Diagnostics;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;

namespace Granit.AI.Anthropic.Internal;

/// <summary>
/// Decorator that emits OpenTelemetry GenAI spans around the inner Anthropic <see cref="IChatClient"/>.
/// Behaviour lives in <see cref="TracingChatClient"/>; this subclass only binds the Anthropic
/// activity source and <c>gen_ai.system</c> identifier.
/// </summary>
internal sealed class TracingAnthropicChatClient(
    IChatClient inner,
    string requestedModel,
    AIProviderCredential credential)
    : TracingChatClient(inner, requestedModel, credential, AIAnthropicActivitySource.TracingProfile);
