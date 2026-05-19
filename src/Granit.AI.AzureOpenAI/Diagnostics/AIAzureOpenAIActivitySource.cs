using System.Diagnostics;

namespace Granit.AI.AzureOpenAI.Diagnostics;

/// <summary>
/// OpenTelemetry activity source for the Azure OpenAI provider.
/// </summary>
/// <remarks>
/// Spans follow the OpenTelemetry GenAI semantic conventions:
/// <c>gen_ai.system</c>, <c>gen_ai.request.model</c>, <c>gen_ai.usage.input_tokens</c>,
/// <c>gen_ai.usage.output_tokens</c>.
/// </remarks>
internal static class AIAzureOpenAIActivitySource
{
    internal const string Name = "Granit.AI.AzureOpenAI";

    /// <summary>Operation name for a non-streaming chat completion.</summary>
    internal const string ChatOperation = "azureopenai.chat";

    /// <summary>Operation name for a streaming chat completion.</summary>
    internal const string ChatStreamOperation = "azureopenai.chat.stream";

    /// <summary>OTel GenAI system identifier for Azure OpenAI.</summary>
    internal const string SystemTagValue = "az.ai.openai";

    internal static readonly ActivitySource Source = new(Name);
}
