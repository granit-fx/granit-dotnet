namespace Granit.Templating.AI;

/// <summary>
/// Generates Scriban template drafts from natural language descriptions.
/// </summary>
/// <remarks>
/// Only data type metadata (property names and types) is sent to the LLM — never business data.
/// On failure, methods return <c>null</c> (graceful degradation).
/// </remarks>
public interface IAITemplateAssistant
{
    /// <summary>
    /// Generates a Scriban HTML template from a description and data schema.
    /// </summary>
    /// <param name="description">Natural language description of the desired template (e.g. "Generate an invoice email template").</param>
    /// <param name="dataType">The data model type whose public properties define the available Scriban variables.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated Scriban HTML template string, or <c>null</c> on failure.</returns>
    Task<string?> GenerateDraftAsync(
        string description,
        Type dataType,
        CancellationToken cancellationToken = default);
}
