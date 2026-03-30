namespace Granit.Privacy.ProcessingPurposes;

/// <summary>
/// Declares a purpose for which personal data is processed, along with its legal basis.
/// Registered at startup via <see cref="GranitPrivacyBuilder.RegisterProcessingPurpose"/>,
/// immutable at runtime.
/// </summary>
/// <param name="PurposeId">Unique identifier (e.g., <c>"marketing-emails"</c>).</param>
/// <param name="DisplayName">Human-readable name (e.g., <c>"Marketing Communications"</c>).</param>
/// <param name="Description">Description of the processing activity.</param>
/// <param name="LegalBasis">Legal basis code (e.g., <c>"CONSENT"</c>, <c>"CONTRACT"</c>).</param>
/// <param name="RequiresExplicitConsent">Whether this purpose requires explicit opt-in consent from the data subject.</param>
/// <param name="DataCategory">Optional data category label (e.g., <c>"contact"</c>, <c>"health"</c>).</param>
public sealed record ProcessingPurposeDefinition(
    string PurposeId,
    string DisplayName,
    string Description,
    string LegalBasis,
    bool RequiresExplicitConsent,
    string? DataCategory = null);
