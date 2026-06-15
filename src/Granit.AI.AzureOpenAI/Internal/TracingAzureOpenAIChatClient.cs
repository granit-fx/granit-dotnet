using Granit.AI.AzureOpenAI.Diagnostics;
using Granit.AI.Diagnostics;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;

namespace Granit.AI.AzureOpenAI.Internal;

/// <summary>
/// Decorator that emits OpenTelemetry GenAI spans around the inner Azure OpenAI <see cref="IChatClient"/>.
/// Behaviour lives in <see cref="TracingChatClient"/>; this subclass only binds the Azure OpenAI
/// activity source and <c>gen_ai.system</c> identifier.
/// </summary>
internal sealed class TracingAzureOpenAIChatClient(
    IChatClient inner,
    string requestedDeployment,
    AIProviderCredential credential)
    : TracingChatClient(inner, requestedDeployment, credential, AIAzureOpenAIActivitySource.TracingProfile);
