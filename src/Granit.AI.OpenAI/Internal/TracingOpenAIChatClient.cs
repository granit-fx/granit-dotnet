using Granit.AI.Diagnostics;
using Granit.AI.OpenAI.Diagnostics;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;

namespace Granit.AI.OpenAI.Internal;

/// <summary>
/// Decorator that emits OpenTelemetry GenAI spans around the inner OpenAI <see cref="IChatClient"/>.
/// Behaviour lives in <see cref="TracingChatClient"/>; this subclass only binds the OpenAI
/// activity source and <c>gen_ai.system</c> identifier.
/// </summary>
internal sealed class TracingOpenAIChatClient(
    IChatClient inner,
    string requestedModel,
    AIProviderCredential credential)
    : TracingChatClient(inner, requestedModel, credential, AIOpenAIActivitySource.TracingProfile);
