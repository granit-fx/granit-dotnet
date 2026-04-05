using Granit.Events;

namespace Granit.Privacy.LegalAgreements.Events;

/// <summary>
/// Published when a legal document version is published or archived,
/// signaling all pods to refresh their in-memory <see cref="ILegalDocumentRegistry"/> cache.
/// </summary>
/// <param name="DocumentId">The legal document identifier that changed.</param>
public sealed record LegalDocumentCacheInvalidatedEto(string DocumentId) : IIntegrationEvent;
