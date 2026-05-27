using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.AI.Extraction.Redaction;

/// <summary>
/// Startup probe that warns when an AI feature has PII redaction enabled in its options
/// but the <see cref="IAIContentRedactor"/> resolved from the container is the identity
/// <see cref="NoOpAIContentRedactor"/>.
/// </summary>
/// <remarks>
/// Shared across every AI-feature package (<c>Granit.LanguageDetection.AI</c>,
/// <c>Granit.Indexing.AI</c>, …) so the "RedactPIIBeforeLLMCall=true while the only
/// registered redactor is the no-op default" trap surfaces loudly at boot rather than
/// during an incident review. Register it through
/// <c>AIRedactionStartupCheckExtensions.AddAIRedactionStartupWarning</c>.
/// Logged at <see cref="LogLevel.Warning"/> rather than failing boot to stay aligned
/// with the framework's graceful-degradation posture.
/// </remarks>
internal sealed partial class AIRedactionStartupCheck(
    string featureName,
    Func<bool> redactionEnabled,
    IAIContentRedactor redactor,
    ILogger<AIRedactionStartupCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (redactionEnabled() && redactor is NoOpAIContentRedactor)
        {
            LogNoOpRedactorWithRedactionEnabled(featureName, redactor.GetType().Name);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{FeatureName}: PII redaction is enabled but the registered IAIContentRedactor is the identity NoOpAIContentRedactor ({RedactorType}). Content will reach the LLM unredacted. Register a real IAIContentRedactor (NER, regex, composite) before the feature's Add… extension to activate masking.")]
    private partial void LogNoOpRedactorWithRedactionEnabled(string featureName, string redactorType);
}
