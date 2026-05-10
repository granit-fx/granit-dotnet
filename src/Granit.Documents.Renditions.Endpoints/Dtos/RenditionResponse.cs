using System;
using Granit.Documents.Renditions.Domain;

namespace Granit.Documents.Renditions.Endpoints.Dtos;

/// <summary>HTTP shape of a single <see cref="DocumentRendition"/> row.</summary>
public sealed record RenditionResponse(
    Guid Id,
    Guid DocumentId,
    Guid DocumentVersionId,
    RenditionType Type,
    string Format,
    RenditionStatus Status,
    long? SizeBytes,
    int? Width,
    int? Height,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason);

/// <summary>Paged-style response for <c>GET /documents/{id}/renditions</c>.</summary>
/// <param name="DocumentId">Identifier of the parent document.</param>
/// <param name="DocumentVersionId">The version that was queried (always the document's current version).</param>
/// <param name="Renditions">Ordered by <see cref="RenditionType"/>, then <see cref="RenditionResponse.Format"/>.</param>
public sealed record ListRenditionsResponse(
    Guid DocumentId,
    Guid DocumentVersionId,
    System.Collections.Generic.IReadOnlyList<RenditionResponse> Renditions);

/// <summary>HTTP shape returned by the rendition download endpoint.</summary>
/// <param name="Url">Short-lived presigned URL the caller can use to fetch the rendition bytes directly from blob storage.</param>
/// <param name="ExpiresAt">UTC expiry of <see cref="Url"/>.</param>
public sealed record RenditionDownloadUrlResponse(Uri Url, DateTimeOffset ExpiresAt);
