using Granit.Core.Events;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published when the GDPR export Saga completes (all fragments received or timeout).
/// </summary>
public sealed record ExportCompletedEto(
    Guid RequestId,
    Guid UserId,
    string ArchiveBlobReferenceId,
    bool IsPartial,
    IReadOnlyList<string> MissingProviders) : IIntegrationEvent;
