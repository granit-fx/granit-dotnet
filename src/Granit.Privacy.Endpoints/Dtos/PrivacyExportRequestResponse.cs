namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Response returned when a personal data export is requested (GDPR Art. 15/20).
/// </summary>
/// <param name="RequestId">Correlation ID for tracking the export saga.</param>
/// <param name="RequestedAt">Timestamp when the export was requested (UTC).</param>
public sealed record PrivacyExportRequestResponse(Guid RequestId, DateTimeOffset RequestedAt);
