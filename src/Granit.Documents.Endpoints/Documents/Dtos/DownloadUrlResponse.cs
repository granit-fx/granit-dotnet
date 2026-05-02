namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Wire-shape response for <c>GET /documents/{id}/download</c>.
/// </summary>
/// <param name="Url">Presigned download URL the client navigates to (or fetches).</param>
/// <param name="ExpiresAt">UTC instant the URL expires.</param>
/// <remarks>
/// JSON over redirect: returning <c>{ url, expiresAt }</c> keeps the response
/// SPA-friendly (the client can decide between <c>window.location</c>, an
/// <c>&lt;a download&gt;</c>, or a fetch + Blob); a 302 redirect would constrain the
/// client to the browser-native download UX.
/// </remarks>
public sealed record DownloadUrlResponse(Uri Url, DateTimeOffset ExpiresAt);
