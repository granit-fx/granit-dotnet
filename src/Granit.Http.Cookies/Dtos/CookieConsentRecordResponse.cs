using Granit.Http.Cookies.Domain;

namespace Granit.Http.Cookies.Dtos;

/// <summary>
/// Summary projection of a <see cref="CookieConsentRecord"/> for list/query endpoints.
/// </summary>
/// <remarks>
/// Returned by the query engine when <c>MapGranitQuery&lt;CookieConsentRecord&gt;</c> is
/// mounted with <c>CookieConsentRecordQueryDefinition</c>. <c>UserAgent</c> is deliberately
/// excluded from the list view — abuse forensics only, no BI value.
/// </remarks>
public sealed record CookieConsentRecordResponse(
    Guid Id,
    Guid? TenantId,
    IReadOnlyList<string> GrantedCategories,
    IReadOnlyList<string> DeniedCategories,
    CookieConsentMode Mode,
    string CmpSource,
    string? AnonymizedIp,
    string? CorrelationId,
    DateTimeOffset DecidedAt);
