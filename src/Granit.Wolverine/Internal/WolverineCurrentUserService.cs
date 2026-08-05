using System.Security.Claims;
using Granit.Users;
using Microsoft.AspNetCore.Http;

namespace Granit.Wolverine.Internal;

/// <summary>
/// <see cref="ICurrentUserService"/> implementation that supports both HTTP context
/// (via <see cref="IHttpContextAccessor"/>) and Wolverine background handler context
/// (via <see cref="AsyncLocal{T}"/> override set by <see cref="Granit.Wolverine.Behaviors.UserContextBehavior"/>).
/// </summary>
/// <remarks>
/// <para>
/// When a Wolverine background handler processes a message, <c>IHttpContextAccessor.HttpContext</c>
/// is null. Without an override, <c>ICurrentUserService.UserId</c> would return null,
/// causing <c>AuditedEntityInterceptor</c> to store <c>ModifiedBy = null</c>.
/// </para>
/// <para>
/// This service resolves properties by checking, in order:
/// <list type="number">
///   <item>The <see cref="AsyncLocal{T}"/> override set by <see cref="Granit.Wolverine.Behaviors.UserContextBehavior"/>.</item>
///   <item>The <c>HttpContext</c> claims (standard HTTP request flow).</item>
/// </list>
/// </para>
/// <para>
/// Registered as a scoped replacement for <see cref="ICurrentUserService"/> by
/// <c>AddGranitWolverine()</c>. The <see cref="IHttpContextAccessor"/> fallback
/// preserves standard HTTP audit trails when the service is used in web request contexts.
/// </para>
/// <para>Compliance: no PII is logged.</para>
/// </remarks>
internal sealed class WolverineCurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService, IWolverineUserContextSetter
{
    private static readonly AsyncLocal<string?> _overrideUserId = new();
    private static readonly AsyncLocal<ActorKind?> _overrideActorKind = new();
    private static readonly AsyncLocal<Guid?> _overrideApiKeyId = new();

    /// <inheritdoc/>
    public IDisposable Change(
        string? userId,
        ActorKind actorKind = ActorKind.User,
        Guid? apiKeyId = null)
    {
        string? previousUserId = _overrideUserId.Value;
        ActorKind? previousActorKind = _overrideActorKind.Value;
        Guid? previousApiKeyId = _overrideApiKeyId.Value;
        _overrideUserId.Value = userId;
        _overrideActorKind.Value = actorKind;
        _overrideApiKeyId.Value = apiKeyId;
        return new UserScope(previousUserId, previousActorKind, previousApiKeyId);
    }

    private ClaimsPrincipal? HttpUser => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc/>
    public string? UserId =>
        _overrideUserId.Value
        ?? HttpUser?.FindFirstValue("sub")
        ?? HttpUser?.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <inheritdoc/>
    public bool IsAuthenticated =>
        _overrideUserId.Value != null || (HttpUser?.Identity?.IsAuthenticated ?? false);

    /// <inheritdoc/>
    public string? UserName =>
        _overrideUserId.Value != null ? null : HttpUser?.Identity?.Name;

    /// <inheritdoc/>
    public string? Email =>
        _overrideUserId.Value != null
            ? null
            : HttpUser?.FindFirstValue(ClaimTypes.Email) ?? HttpUser?.FindFirstValue("email");

    /// <inheritdoc/>
    public string? FirstName =>
        _overrideUserId.Value != null
            ? null
            : HttpUser?.FindFirstValue(ClaimTypes.GivenName) ?? HttpUser?.FindFirstValue("given_name");

    /// <inheritdoc/>
    public string? LastName =>
        _overrideUserId.Value != null
            ? null
            : HttpUser?.FindFirstValue(ClaimTypes.Surname) ?? HttpUser?.FindFirstValue("family_name");

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRoles() =>
        _overrideUserId.Value != null
            ? []
            : (IReadOnlyList<string>)(HttpUser?.FindAll(ClaimTypes.Role)
                  .Select(c => c.Value)
                  .ToList() ?? []);

    /// <inheritdoc/>
    public bool IsInRole(string role) =>
        _overrideUserId.Value == null && (HttpUser?.IsInRole(role) ?? false);

    /// <inheritdoc/>
    public ActorKind ActorKind =>
        _overrideActorKind.Value
        ?? (HttpUser?.FindFirstValue("actor_kind") is { } ak && Enum.TryParse<ActorKind>(ak, out ActorKind parsed)
            ? parsed
            : ActorKind.User);

    /// <inheritdoc/>
    public bool IsMachine => ActorKind is not ActorKind.User;

    /// <inheritdoc/>
    public Guid? ApiKeyId =>
        _overrideApiKeyId.Value
        ?? (HttpUser?.FindFirstValue("api_key_id") is { } id && Guid.TryParse(id, out Guid parsed)
            ? parsed
            : null);

    private sealed class UserScope(
        string? previousUserId,
        ActorKind? previousActorKind,
        Guid? previousApiKeyId) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _overrideUserId.Value = previousUserId;
                _overrideActorKind.Value = previousActorKind;
                _overrideApiKeyId.Value = previousApiKeyId;
            }
        }
    }
}
