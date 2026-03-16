namespace Granit.Privacy.AI;

/// <summary>
/// Scans text for personally identifiable information (PII) using AI.
/// </summary>
/// <remarks>
/// Implementations must never log the actual text content to prevent PII leakage.
/// The AI workspace used for detection should be configured with a local model (Ollama)
/// or a provider covered by a Data Processing Agreement to ensure PII data does not
/// leave the security perimeter.
/// </remarks>
public interface IAIPiiDetector
{
    /// <summary>
    /// Scans the provided text for PII.
    /// </summary>
    /// <param name="text">The text to scan. Must not be <c>null</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detection result indicating whether PII was found and what types were detected.</returns>
    Task<PiiDetectionResult> ScanAsync(string text, CancellationToken cancellationToken = default);
}
