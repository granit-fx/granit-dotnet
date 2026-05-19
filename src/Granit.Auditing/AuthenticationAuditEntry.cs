using Granit.Auditing.Domain;

namespace Granit.Auditing;

/// <summary>
/// Builds <see cref="AuditEntry"/> instances for authentication events
/// (login attempts, token issuance, denials) — the events the EF Core change-tracking
/// interceptor cannot observe because no entity is mutated.
/// </summary>
/// <remarks>
/// <para>
/// Successful authentications land in <see cref="AuditCategory.PrivilegedAccess"/>
/// (ISO 27001 A.12.4.3 — "what privileged actors successfully did") and failed
/// attempts in <see cref="AuditCategory.AccessDenied"/> (A.12.4.1). Both default
/// to ~7 year retention via <see cref="Options.AuditingOptions"/>.
/// </para>
/// <para>
/// Authentication details are persisted as a synthetic
/// <see cref="AuditEntityChange"/> with <c>EntityType = "Authentication"</c>
/// so investigators get a self-contained row without correlating against logs.
/// Method, outcome and (when available) failure reason are recorded as
/// <see cref="AuditPropertyChange"/> rows under that change.
/// </para>
/// </remarks>
public static class AuthenticationAuditEntry
{
    /// <summary>
    /// Synthetic entity type recorded on the audit row's
    /// <see cref="AuditEntityChange.EntityType"/>.
    /// </summary>
    public const string SyntheticEntityType = "Authentication";

    /// <summary>
    /// Sentinel <see cref="AuditEntry.UserId"/> for failed attempts where the
    /// caller could not be identified (e.g. "user_not_found").
    /// </summary>
    public const string UnknownUserSentinel = "<unknown>";

    /// <summary>
    /// Builds a <see cref="AuditCategory.PrivilegedAccess"/> entry recording a
    /// successful authentication or token issuance.
    /// </summary>
    /// <param name="timestamp">Event time (UTC).</param>
    /// <param name="userId">
    /// Authenticated subject identifier. Required — successful auth always
    /// resolves a user.
    /// </param>
    /// <param name="userName">Display name (nullable for pseudonymization).</param>
    /// <param name="method">
    /// Authentication or grant type — e.g. <c>"password"</c>, <c>"totp"</c>,
    /// <c>"recovery_code"</c>, <c>"oidc"</c>, <c>"authorization_code"</c>,
    /// <c>"refresh_token"</c>.
    /// </param>
    /// <param name="tenantId">Tenant scope of the operation.</param>
    /// <param name="ipAddress">Source IP address.</param>
    /// <param name="userAgent">Client User-Agent header.</param>
    /// <param name="correlationId">Distributed-tracing correlation id.</param>
    public static AuditEntry CreateSuccess(
        DateTimeOffset timestamp,
        string userId,
        string? userName,
        string method,
        Guid? tenantId,
        string? ipAddress,
        string? userAgent,
        string? correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(method);

        return Build(
            timestamp,
            userId,
            userName,
            method,
            reason: null,
            AuditCategory.PrivilegedAccess,
            tenantId,
            ipAddress,
            userAgent,
            correlationId);
    }

    /// <summary>
    /// Builds an <see cref="AuditCategory.AccessDenied"/> entry recording a
    /// failed authentication or token issuance.
    /// </summary>
    /// <param name="timestamp">Event time (UTC).</param>
    /// <param name="userId">
    /// Resolved subject identifier, or <see langword="null"/> when the user
    /// could not be identified. Falls back to <see cref="UnknownUserSentinel"/>.
    /// </param>
    /// <param name="userName">Display name (nullable).</param>
    /// <param name="method">
    /// Attempted authentication or grant type — see <see cref="CreateSuccess"/>.
    /// </param>
    /// <param name="reason">
    /// Short failure code — e.g. <c>"invalid_credentials"</c>,
    /// <c>"account_locked"</c>, <c>"token_exchange_failed"</c>. Mirrors the
    /// failure label used by the matching metric so dashboards and audit rows
    /// stay correlatable.
    /// </param>
    /// <param name="tenantId">Tenant scope (when known).</param>
    /// <param name="ipAddress">Source IP address.</param>
    /// <param name="userAgent">Client User-Agent header.</param>
    /// <param name="correlationId">Distributed-tracing correlation id.</param>
    public static AuditEntry CreateFailure(
        DateTimeOffset timestamp,
        string? userId,
        string? userName,
        string method,
        string reason,
        Guid? tenantId,
        string? ipAddress,
        string? userAgent,
        string? correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        return Build(
            timestamp,
            string.IsNullOrEmpty(userId) ? UnknownUserSentinel : userId,
            userName,
            method,
            reason,
            AuditCategory.AccessDenied,
            tenantId,
            ipAddress,
            userAgent,
            correlationId);
    }

    private static AuditEntry Build(
        DateTimeOffset timestamp,
        string userId,
        string? userName,
        string method,
        string? reason,
        AuditCategory category,
        Guid? tenantId,
        string? ipAddress,
        string? userAgent,
        string? correlationId)
    {
        List<AuditPropertyChange> properties =
        [
            new AuditPropertyChange { PropertyName = "Method", NewValue = method },
            new AuditPropertyChange
            {
                PropertyName = "Outcome",
                NewValue = category == AuditCategory.PrivilegedAccess ? "success" : "failure",
            },
        ];

        if (!string.IsNullOrEmpty(reason))
        {
            properties.Add(new AuditPropertyChange { PropertyName = "Reason", NewValue = reason });
        }

        AuditEntityChange change = new()
        {
            EntityType = SyntheticEntityType,
            EntityId = userId,
            ChangeType = AuditChangeType.Created,
            PropertyChanges = properties,
        };

        return new AuditEntry
        {
            Timestamp = timestamp,
            UserId = userId,
            UserName = userName,
            Category = category,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            TenantId = tenantId,
            CorrelationId = correlationId,
            EntityChanges = [change],
        };
    }
}
