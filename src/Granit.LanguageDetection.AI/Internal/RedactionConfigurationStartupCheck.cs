using Granit.AI.Extraction.Redaction;
using Granit.LanguageDetection.AI.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.LanguageDetection.AI.Internal;

/// <summary>
/// Startup probe that warns when <see cref="LanguageDetectionAIOptions.RedactPIIBeforeLLMCall"/>
/// is enabled but the registered <see cref="IAIContentRedactor"/> is the framework's
/// identity <see cref="NoOpAIContentRedactor"/>.
/// </summary>
/// <remarks>
/// Without this probe, hosts can ship a configuration where <c>RedactPIIBeforeLLMCall=true</c>
/// looks like it activates PII masking, while the actual redactor in the container is
/// the no-op default — sensitive content flows raw to the LLM and the operator gets no
/// signal until an incident review. Logged at <see cref="LogLevel.Warning"/> rather
/// than failing boot to stay aligned with the "graceful fall-through" contract: an
/// over-restrictive default would push hosts to disable the seam outright.
/// </remarks>
internal sealed partial class RedactionConfigurationStartupCheck(
    IOptions<LanguageDetectionAIOptions> options,
    IAIContentRedactor redactor,
    ILogger<RedactionConfigurationStartupCheck> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.RedactPIIBeforeLLMCall && redactor is NoOpAIContentRedactor)
        {
            LogNoOpRedactorWithFlagEnabled(redactor.GetType().Name);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Granit.LanguageDetection.AI: RedactPIIBeforeLLMCall is enabled but the registered IAIContentRedactor is the identity NoOpAIContentRedactor ({RedactorType}). Content will reach the LLM unredacted. Register a real IAIContentRedactor (NER, regex, composite) before AddGranitLanguageDetectionAI() to activate masking.")]
    private partial void LogNoOpRedactorWithFlagEnabled(string redactorType);
}
