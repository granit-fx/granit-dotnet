using System.Diagnostics;

namespace Granit.AI.AzureOpenAI.Diagnostics;

/// <summary>
/// OpenTelemetry activity source for the Azure OpenAI provider.
/// </summary>
internal static class AIAzureOpenAIActivitySource
{
    internal const string Name = "Granit.AI.AzureOpenAI";

    internal static readonly ActivitySource Source = new(Name);
}
