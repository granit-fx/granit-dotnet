using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Granit.Diagnostics;
using Granit.Events;
using Granit.Identity.Diagnostics;
using Granit.Identity.Events;
using Granit.Identity.Federated.Exceptions;
using Granit.Identity.Federated.Keycloak.Diagnostics;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// <see cref="IIdentityProvider"/> implementation that queries the Keycloak Admin REST API.
/// </summary>
/// <remarks>
/// <para>
/// Read operations follow graceful degradation: if Keycloak is unreachable, logs a warning
/// and returns empty results instead of propagating the exception.
/// </para>
/// <para>
/// Write operations (<see cref="SetUserEnabledAsync"/>) propagate exceptions so callers can
/// handle failures explicitly.
/// </para>
/// </remarks>
internal sealed partial class KeycloakIdentityProvider(
    KeycloakAdminTokenService tokenService,
    KeycloakUserTokenExchangeService tokenExchangeService,
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    IDistributedEventBus distributedEventBus,
    IdentityMetrics metrics,
    ILogger<KeycloakIdentityProvider> logger)
    : IIdentityProvider, IIdentityClientRoleManager, IUserSessionProvider, IUserDeviceProvider
{
    private const string ProviderName = "keycloak";
    private const string GetUserOperation = "get_user";
    private const string ListUsersOperation = "list_users";

    /// <summary>
    /// Keycloak client attribute (operator-set on the client in Keycloak) declaring the device kind of the
    /// devices that authenticate through it — the federated equivalent of the OpenIddict application's declared
    /// kind. Value is a <see cref="DeviceKind"/> name (e.g. <c>"MobileApp"</c>, <c>"Tv"</c>).
    /// </summary>
    private const string DeviceKindClientAttribute = "granit.device_kind";

    /// <summary>
    /// Detects authorisation failures wrapped in <see cref="HttpRequestException"/>.
    /// 401/403 from the Keycloak admin API mean either the service-account token has
    /// expired or its <c>realm-management</c> role grants were revoked — both must be
    /// surfaced to the caller rather than silently degrading to "no users".
    /// </summary>
    private static bool IsAuthorizationFailure(HttpRequestException ex) =>
        ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    /// <summary>
    /// Classifies a non-authorization admin-API fault into the taxonomy the graceful-degradation
    /// decorator understands: HTTP 429 → <see cref="IdentityProviderThrottledException"/>, everything
    /// else (5xx, timeout, connection reset) → <see cref="IdentityProviderTransientException"/>.
    /// Applied only to the decorated <see cref="IIdentityProvider"/> read methods; the caller must
    /// re-throw genuine cancellation before reaching here.
    /// </summary>
    private static Exception ClassifyReadFault(Exception ex, string operation) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } =>
            new IdentityProviderUnauthorizedException(ProviderName, operation, ex),
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } =>
            new IdentityProviderThrottledException(ProviderName, operation, retryAfter: null, ex),
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } =>
            new IdentityProviderNotFoundException(ProviderName, operation, ex),
        _ => new IdentityProviderTransientException(ProviderName, operation, ex),
    };

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null,
        int? first = null,
        int? max = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUsers);
        activity?.SetTag(IdentityKeycloakActivitySource.TagHasSearch, !string.IsNullOrWhiteSpace(search));

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetUsersEndpoint(search, first, max);

            List<KeycloakUserRepresentation>? users = await client
                .GetFromJsonAsync<List<KeycloakUserRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return users?.ConvertAll(ToIdentityUser) ?? [];
        }
        catch (HttpRequestException ex) when (IsAuthorizationFailure(ex))
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.RecordOperationError(null, ListUsersOperation, ProviderName);
            throw new IdentityProviderUnauthorizedException(ProviderName, ListUsersOperation, ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.RecordOperationError(null, ListUsersOperation, ProviderName);
            LogKeycloakGetUsersFailed(ex);
            throw ClassifyReadFault(ex, ListUsersOperation);
        }
    }

    /// <inheritdoc/>
    public async Task<IIdentityUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUser);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetUserEndpoint(userId);

            KeycloakUserRepresentation? user = await client
                .GetFromJsonAsync<KeycloakUserRepresentation>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            metrics.RecordOperationCompleted(null, GetUserOperation, ProviderName, user is not null ? "found" : "not_found");
            metrics.RecordOperationDuration(null, GetUserOperation, ProviderName, Stopwatch.GetElapsedTime(startTimestamp));

            return user is not null ? ToIdentityUser(user) : null;
        }
        catch (HttpRequestException ex) when (IsAuthorizationFailure(ex))
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.RecordOperationError(null, GetUserOperation, ProviderName);
            throw new IdentityProviderUnauthorizedException(ProviderName, GetUserOperation, ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // A single-user GET returning 404 is a normal negative result, not a fault.
            metrics.RecordOperationCompleted(null, GetUserOperation, ProviderName, "not_found");
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            metrics.RecordOperationError(null, GetUserOperation, ProviderName);
            LogKeycloakGetUserFailed(ex, userId);
            throw ClassifyReadFault(ex, GetUserOperation);
        }
    }

    /// <inheritdoc/>
    public async Task SetUserEnabledAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.SetUserEnabled);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagEnabled, enabled);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetUserEndpoint(userId);

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            endpoint, new { enabled }, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogUserEnabledChanged(userId, enabled ? "enabled" : "disabled");

        await distributedEventBus.PublishAsync(new IdentityUserEnabledChangedEto(userId, enabled), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateUserAsync(
        string userId,
        IdentityUserUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(update);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.UpdateUser);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetUserEndpoint(userId);

        // Keycloak PUT /admin/realms/{realm}/users/{id} expects the full UserRepresentation.
        // We first GET the current representation, patch the requested fields, then PUT back.
        KeycloakUserRepresentation? current = await client
            .GetFromJsonAsync<KeycloakUserRepresentation>(endpoint, cancellationToken)
            .ConfigureAwait(false);

        if (current is null)
        {
            throw new HttpRequestException($"User {userId} not found in Keycloak.");
        }

        // Merge custom attributes: apply only the keys provided in the update,
        // preserving all other existing attributes on the Keycloak user.
        Dictionary<string, List<string>>? mergedAttributes = current.Attributes;

        if (update.Attributes is { Count: > 0 })
        {
            mergedAttributes = current.Attributes is not null
                ? new Dictionary<string, List<string>>(current.Attributes, StringComparer.Ordinal)
                : new Dictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string?> attr in update.Attributes)
            {
                if (attr.Value is null)
                {
                    mergedAttributes.Remove(attr.Key);
                }
                else
                {
                    mergedAttributes[attr.Key] = [attr.Value];
                }
            }
        }

        KeycloakUserRepresentation updated = current with
        {
            Email = update.Email ?? current.Email,
            FirstName = update.FirstName ?? current.FirstName,
            LastName = update.LastName ?? current.LastName,
            Attributes = mergedAttributes,
        };

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            endpoint, updated, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogUserProfileUpdated(userId);

        metrics.RecordOperationCompleted(null, "update_user", ProviderName, "updated");

        await distributedEventBus.PublishAsync(new IdentityUserProfileUpdatedEto(userId, update), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserSessionDescriptor>> ListAsync(
        string userId,
        string? currentSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUserSessions);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        try
        {
            List<KeycloakSessionRepresentation> sessions = await GetSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
            return sessions.ConvertAll(s => ToSessionDescriptor(s, userId, currentSessionId));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetSessionsFailed(ex, userId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserDevice>> ListAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUserDeviceActivity);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        try
        {
            return options.Value.UseTokenExchangeForDeviceActivity
                ? await GetDevicesViaAccountApiAsync(userId, cancellationToken).ConfigureAwait(false)
                : await GetDevicesViaAdminSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetDeviceActivityFailed(ex, userId);
            return [];
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Keycloak revokes a single user session via <c>DELETE /admin/realms/{realm}/sessions/{id}</c>.
    /// Publishes <see cref="UserSessionsRevokedEto"/> on success so downstream session caches
    /// invalidate, mirroring the previous bridge behaviour.
    /// </remarks>
    public async Task<bool> RevokeAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(sessionId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.TerminateSession);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetSessionEndpoint(sessionId);

        using HttpResponseMessage response = await client
            .DeleteAsync(endpoint, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogSessionTerminated(sessionId, userId);

        await distributedEventBus.PublishAsync(new UserSessionsRevokedEto(userId), cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Lists the user's Keycloak sessions and revokes each one whose id differs from
    /// <paramref name="currentSessionId"/>. Returns the number of sessions revoked.
    /// </remarks>
    public async Task<int> RevokeOthersAsync(
        string userId,
        string currentSessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(currentSessionId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.TerminateAllSessions);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        long startTimestamp = Stopwatch.GetTimestamp();

        List<KeycloakSessionRepresentation> sessions = await GetSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        int revoked = 0;
        foreach (string sessionId in sessions.Select(s => s.Id))
        {
            if (string.Equals(sessionId, currentSessionId, StringComparison.Ordinal))
            {
                continue;
            }

            if (await RevokeAsync(userId, sessionId, cancellationToken).ConfigureAwait(false))
            {
                revoked++;
            }
        }

        LogOtherSessionsTerminated(userId, revoked);

        metrics.RecordOperationCompleted(null, "terminate_all_sessions", ProviderName, "terminated");
        metrics.RecordOperationDuration(null, "terminate_all_sessions", ProviderName, Stopwatch.GetElapsedTime(startTimestamp));

        return revoked;
    }

    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetPasswordChangedAtAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetPasswordChangedAt);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetUserCredentialsEndpoint(userId);

            List<KeycloakCredentialRepresentation>? credentials = await client
                .GetFromJsonAsync<List<KeycloakCredentialRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            KeycloakCredentialRepresentation? passwordCred = credentials?
                .Find(c => string.Equals(c.Type, "password", StringComparison.OrdinalIgnoreCase));

            return passwordCred?.CreatedDate is long ms
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetCredentialsFailed(ex, userId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetRoles);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetRolesEndpoint();

            List<KeycloakRoleRepresentation>? roles = await client
                .GetFromJsonAsync<List<KeycloakRoleRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return roles?.ConvertAll(r => new IdentityRole(r.Id, r.Name, r.Description)) ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetRolesFailed(ex);
            throw ClassifyReadFault(ex, "list_roles");
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleName);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetRoleMembers);
        activity?.SetTag(IdentityKeycloakActivitySource.TagRoleName, roleName);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetRoleUsersEndpoint(roleName);

            List<KeycloakUserRepresentation>? users = await client
                .GetFromJsonAsync<List<KeycloakUserRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return users?.ConvertAll(ToIdentityUser) ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetRoleMembersFailed(ex, roleName);
            throw ClassifyReadFault(ex, "list_role_members");
        }
    }

    // ──── Feature 1: User role management ────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUserRoles);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetUserRealmRoleMappingsEndpoint(userId);

            List<KeycloakRoleRepresentation>? roles = await client
                .GetFromJsonAsync<List<KeycloakRoleRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return roles?.ConvertAll(r => new IdentityRole(r.Id, r.Name, r.Description)) ?? [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetUserRolesFailed(ex, userId);
            throw ClassifyReadFault(ex, "list_user_roles");
        }
    }

    /// <inheritdoc/>
    public async Task AssignRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(roleName);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.AssignRole);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagRoleName, roleName);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);

        KeycloakRoleRepresentation role = await GetRoleByNameAsync(client, roleName, cancellationToken).ConfigureAwait(false);

        string endpoint = options.Value.GetUserRealmRoleMappingsEndpoint(userId);
        using HttpResponseMessage response = await client
            .PostAsJsonAsync(endpoint, new[] { role }, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogRoleAssigned(roleName, userId);

        metrics.RecordOperationCompleted(null, "assign_role", ProviderName, "assigned");

        await distributedEventBus.PublishAsync(new IdentityRoleAssignedEto(userId, roleName), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(roleName);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.RemoveRole);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagRoleName, roleName);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);

        KeycloakRoleRepresentation role = await GetRoleByNameAsync(client, roleName, cancellationToken).ConfigureAwait(false);

        string endpoint = options.Value.GetUserRealmRoleMappingsEndpoint(userId);

        using HttpRequestMessage request = new(HttpMethod.Delete, endpoint)
        {
            Content = JsonContent.Create(new[] { role })
        };

        using HttpResponseMessage response = await client
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogRoleRemoved(roleName, userId);

        await distributedEventBus.PublishAsync(new IdentityRoleRemovedEto(userId, roleName), cancellationToken).ConfigureAwait(false);
    }

    // ──── Phase 2: Client role support (IIdentityClientRoleManager) ────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetClientsAsync(
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetClients);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetClientsEndpoint();

            List<KeycloakClientRepresentation>? clients = await client
                .GetFromJsonAsync<List<KeycloakClientRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return clients?.ConvertAll(c => c.ClientId) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetClientsFailed(ex);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetClientRolesAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetClientRoles);
        activity?.SetTag(IdentityKeycloakActivitySource.TagClientId, clientId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string clientUuid = await ResolveClientUuidAsync(client, clientId, cancellationToken).ConfigureAwait(false);

        try
        {
            string endpoint = options.Value.GetClientRolesEndpoint(clientUuid);
            List<KeycloakRoleRepresentation>? roles = await client
                .GetFromJsonAsync<List<KeycloakRoleRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return roles?.ConvertAll(r => new IdentityRole(r.Id, r.Name, r.Description) { ClientId = clientId }) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetClientRolesFailed(ex, clientId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetUserClientRolesAsync(
        string userId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUserClientRoles);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagClientId, clientId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string clientUuid = await ResolveClientUuidAsync(client, clientId, cancellationToken).ConfigureAwait(false);

        try
        {
            string endpoint = options.Value.GetUserClientRoleMappingsEndpoint(userId, clientUuid);
            List<KeycloakRoleRepresentation>? roles = await client
                .GetFromJsonAsync<List<KeycloakRoleRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return roles?.ConvertAll(r => new IdentityRole(r.Id, r.Name, r.Description) { ClientId = clientId }) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetUserClientRolesFailed(ex, userId, clientId);
            return [];
        }
    }

    private async Task<string> ResolveClientUuidAsync(
        HttpClient client,
        string clientId,
        CancellationToken cancellationToken)
    {
        string endpoint = options.Value.GetClientsEndpoint(clientId);
        List<KeycloakClientRepresentation>? clients = await client
            .GetFromJsonAsync<List<KeycloakClientRepresentation>>(endpoint, cancellationToken)
            .ConfigureAwait(false);

        KeycloakClientRepresentation? match = clients?.FirstOrDefault(c =>
            string.Equals(c.ClientId, clientId, StringComparison.Ordinal));

        return match?.Id ?? throw new KeycloakClientNotFoundException(clientId);
    }

    // ──── Phase 3: Client-role writes (ADR-031) ────

    /// <inheritdoc/>
    public async Task<IdentityRole> CreateClientRoleAsync(
        string clientId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.CreateClientRole);
        activity?.SetTag(IdentityKeycloakActivitySource.TagClientId, clientId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string clientUuid = await ResolveClientUuidAsync(client, clientId, cancellationToken).ConfigureAwait(false);

        KeycloakRoleRepresentation payload = new(
            Id: string.Empty, // server-assigned
            Name: name,
            Description: description)
        {
            ContainerId = clientUuid,
            ClientRole = true,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            options.Value.GetClientRolesEndpoint(clientUuid), payload, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Keycloak returns 201 Created with an empty body; re-fetch the role to pick up its id.
        KeycloakRoleRepresentation? created = await client
            .GetFromJsonAsync<KeycloakRoleRepresentation>(
                options.Value.GetClientRoleByNameEndpoint(clientUuid, name), cancellationToken)
            .ConfigureAwait(false);

        if (created is null)
        {
            throw new InvalidOperationException(
                $"Keycloak created client role '{name}' on '{clientId}' but did not return it on re-fetch.");
        }

        return new IdentityRole(created.Id, created.Name, created.Description) { ClientId = clientId };
    }

    /// <inheritdoc/>
    public async Task AssignClientRoleAsync(
        string userId,
        string clientId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.AssignClientRole);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagClientId, clientId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string clientUuid = await ResolveClientUuidAsync(client, clientId, cancellationToken).ConfigureAwait(false);
        KeycloakRoleRepresentation role = await LookupClientRoleAsync(client, clientUuid, clientId, roleName, cancellationToken).ConfigureAwait(false);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            options.Value.GetUserClientRoleMappingsEndpoint(userId, clientUuid),
            new[] { role },
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task RemoveClientRoleAsync(
        string userId,
        string clientId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.RemoveClientRole);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagClientId, clientId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string clientUuid = await ResolveClientUuidAsync(client, clientId, cancellationToken).ConfigureAwait(false);
        KeycloakRoleRepresentation role = await LookupClientRoleAsync(client, clientUuid, clientId, roleName, cancellationToken).ConfigureAwait(false);

        HttpRequestMessage request = new(HttpMethod.Delete,
            options.Value.GetUserClientRoleMappingsEndpoint(userId, clientUuid))
        {
            Content = System.Net.Http.Json.JsonContent.Create(new[] { role }),
        };
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<KeycloakRoleRepresentation> LookupClientRoleAsync(
        HttpClient client,
        string clientUuid,
        string clientId,
        string roleName,
        CancellationToken cancellationToken)
    {
        KeycloakRoleRepresentation? role = await client
            .GetFromJsonAsync<KeycloakRoleRepresentation>(
                options.Value.GetClientRoleByNameEndpoint(clientUuid, roleName), cancellationToken)
            .ConfigureAwait(false);

        return role ?? throw new InvalidOperationException(
            $"Keycloak client role '{roleName}' not found on client '{clientId}'.");
    }

    // ──── Feature 3: Password reset ────

    /// <inheritdoc/>
    public async Task SendPasswordResetEmailAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.SendPasswordResetEmail);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetExecuteActionsEmailEndpoint(userId);

        using HttpResponseMessage response = await client
            .PutAsJsonAsync(endpoint, new[] { "UPDATE_PASSWORD" }, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogPasswordResetEmailSent(userId);

        await distributedEventBus.PublishAsync(new IdentityPasswordResetEto(userId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetTemporaryPasswordAsync(
        string userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(temporaryPassword);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.SetTemporaryPassword);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetResetPasswordEndpoint(userId);

        using HttpResponseMessage response = await client.PutAsJsonAsync(
            endpoint,
            new { type = "password", value = temporaryPassword, temporary = true },
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogTemporaryPasswordSet(userId);

        await distributedEventBus.PublishAsync(new IdentityPasswordResetEto(userId), cancellationToken).ConfigureAwait(false);
    }

    // ──── Feature 4: User creation ────

    /// <inheritdoc/>
    public async Task<IIdentityUser> CreateUserAsync(
        IdentityUserCreate user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.CreateUser);
        long startTimestamp = Stopwatch.GetTimestamp();

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetUsersEndpoint();

        var payload = new
        {
            username = user.Username,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            enabled = user.Enabled
        };

        using HttpResponseMessage createResponse = await client
            .PostAsJsonAsync(endpoint, payload, cancellationToken)
            .ConfigureAwait(false);

        createResponse.EnsureSuccessStatusCode();

        // Extract the created user ID from the Location header
        string locationHeader = createResponse.Headers.Location?.AbsolutePath
            ?? throw new InvalidOperationException("Keycloak did not return a Location header after user creation.");

        string createdUserId = locationHeader[(locationHeader.LastIndexOf('/') + 1)..];

        // Set temporary password if provided
        if (!string.IsNullOrEmpty(user.TemporaryPassword))
        {
            await SetTemporaryPasswordAsync(createdUserId, user.TemporaryPassword, cancellationToken)
                .ConfigureAwait(false);
        }

        LogUserCreated(LogRedaction.Username(user.Username), createdUserId);

        metrics.RecordOperationCompleted(null, "create_user", ProviderName, "created");
        metrics.RecordOperationDuration(null, "create_user", ProviderName, Stopwatch.GetElapsedTime(startTimestamp));

        await distributedEventBus.PublishAsync(new IdentityUserCreatedEto(createdUserId, user.Username, user.Email), cancellationToken).ConfigureAwait(false);

        return new FederatedIdentityUser(
            createdUserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Enabled);
    }

    // ──── Feature 5: Group management ────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetGroups);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetGroupsEndpoint();

            List<KeycloakGroupRepresentation>? groups = await client
                .GetFromJsonAsync<List<KeycloakGroupRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return groups?.ConvertAll(ToIdentityGroup) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetGroupsFailed(ex);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUserGroups);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetUserGroupsEndpoint(userId);

            List<KeycloakGroupRepresentation>? groups = await client
                .GetFromJsonAsync<List<KeycloakGroupRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return groups?.ConvertAll(ToIdentityGroup) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogKeycloakGetUserGroupsFailed(ex, userId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task AddUserToGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(groupId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.AddUserToGroup);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagGroupId, groupId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetUserGroupMembershipEndpoint(userId, groupId);

        using HttpResponseMessage response = await client
            .PutAsync(endpoint, content: null, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogUserAddedToGroup(userId, groupId);

        await distributedEventBus.PublishAsync(new IdentityGroupMembershipChangedEto(userId, groupId, Added: true), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveUserFromGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(groupId);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.RemoveUserFromGroup);
        activity?.SetTag(IdentityKeycloakActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityKeycloakActivitySource.TagGroupId, groupId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetUserGroupMembershipEndpoint(userId, groupId);

        using HttpResponseMessage response = await client
            .DeleteAsync(endpoint, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        LogUserRemovedFromGroup(userId, groupId);

        await distributedEventBus.PublishAsync(new IdentityGroupMembershipChangedEto(userId, groupId, Added: false), cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<KeycloakSessionRepresentation>> GetSessionsAsync(
        string userId, CancellationToken cancellationToken)
    {
        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = options.Value.GetUserSessionsEndpoint(userId);

        List<KeycloakSessionRepresentation>? sessions = await client
            .GetFromJsonAsync<List<KeycloakSessionRepresentation>>(endpoint, cancellationToken)
            .ConfigureAwait(false);

        return sessions ?? [];
    }

    private async Task<IReadOnlyList<UserDevice>> GetDevicesViaAccountApiAsync(
        string userId, CancellationToken cancellationToken)
    {
        string userToken = await tokenExchangeService
            .ExchangeTokenForUserAsync(userId, cancellationToken).ConfigureAwait(false);

        HttpClient client = httpClientFactory.CreateClient("KeycloakAdmin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        string endpoint = options.Value.GetAccountSessionsDevicesEndpoint();

        List<KeycloakDeviceRepresentation>? devices = await client
            .GetFromJsonAsync<List<KeycloakDeviceRepresentation>>(endpoint, cancellationToken)
            .ConfigureAwait(false);

        return devices?.ConvertAll(ToUserDevice) ?? [];
    }

    private async Task<IReadOnlyList<UserDevice>> GetDevicesViaAdminSessionsAsync(
        string userId, CancellationToken cancellationToken)
    {
        List<KeycloakSessionRepresentation> sessions = await GetSessionsAsync(userId, cancellationToken).ConfigureAwait(false);
        if (sessions.Count == 0)
        {
            return [];
        }

        // The admin sessions endpoint exposes no device metadata, so each session maps to its own device keyed
        // by the session id. The kind comes from the Keycloak client the session authenticated through, when
        // that client declares one; otherwise Browser.
        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, DeviceKind> kindByClientUuid = new(StringComparer.Ordinal);

        var devices = new List<UserDevice>(sessions.Count);
        foreach (KeycloakSessionRepresentation session in sessions)
        {
            DeviceKind kind = await ResolveSessionDeviceKindAsync(
                client, session.Clients, kindByClientUuid, cancellationToken).ConfigureAwait(false);
            devices.Add(new UserDevice(
                DeviceId: session.Id,
                Kind: kind,
                OperatingSystem: null,
                Browser: null,
                LastSeen: DateTimeOffset.FromUnixTimeMilliseconds(session.LastAccess),
                SessionCount: 1,
                LastLocation: null));
        }

        return devices;
    }

    // The Keycloak client a session authenticated through declares its device kind via the client attribute
    // "granit.device_kind" (e.g. "MobileApp" / "Tv"). A session can span several clients; the most specific
    // declared kind wins. Absent / unparseable / Unknown — or any lookup failure — falls back to Browser, since
    // an IdP-SSO session is browser-based unless the client says otherwise. Operators set the attribute on the
    // client in Keycloak (Granit does not manage federated client config), so this path is read-only.
    private async ValueTask<DeviceKind> ResolveSessionDeviceKindAsync(
        HttpClient client,
        Dictionary<string, string>? clients,
        Dictionary<string, DeviceKind> cache,
        CancellationToken cancellationToken)
    {
        if (clients is not { Count: > 0 })
        {
            return DeviceKind.Browser;
        }

        foreach (string clientUuid in clients.Keys)
        {
            DeviceKind kind = await ResolveClientDeviceKindAsync(client, clientUuid, cache, cancellationToken)
                .ConfigureAwait(false);
            if (kind is not DeviceKind.Browser)
            {
                return kind;
            }
        }

        return DeviceKind.Browser;
    }

    private async ValueTask<DeviceKind> ResolveClientDeviceKindAsync(
        HttpClient client,
        string clientUuid,
        Dictionary<string, DeviceKind> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(clientUuid, out DeviceKind cached))
        {
            return cached;
        }

        DeviceKind resolved = DeviceKind.Browser;
        try
        {
            KeycloakClientRepresentation? rep = await client
                .GetFromJsonAsync<KeycloakClientRepresentation>(
                    options.Value.GetClientByUuidEndpoint(clientUuid), cancellationToken)
                .ConfigureAwait(false);

            if (rep?.Attributes is { } attributes
                && attributes.TryGetValue(DeviceKindClientAttribute, out string? raw)
                && Enum.TryParse(raw, ignoreCase: false, out DeviceKind kind)
                && Enum.IsDefined(kind)
                && kind != DeviceKind.Unknown)
            {
                resolved = kind;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            // A client we cannot read (gone, forbidden, malformed) must not break device listing — default Browser.
            LogKeycloakClientDeviceKindUnresolved(ex, clientUuid);
        }

        cache[clientUuid] = resolved;
        return resolved;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        string token = await tokenService.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        HttpClient client = httpClientFactory.CreateClient("KeycloakAdmin");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static FederatedIdentityUser ToIdentityUser(KeycloakUserRepresentation user) =>
        new(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.Enabled,
            FlattenAttributes(user.Attributes));

    private static Dictionary<string, string>? FlattenAttributes(
        Dictionary<string, List<string>>? attributes)
    {
        if (attributes is not { Count: > 0 })
        {
            return null;
        }

        Dictionary<string, string> result = new(attributes.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<string>> kvp in attributes)
        {
            if (kvp.Value is [var first, ..])
            {
                result[kvp.Key] = first;
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static UserSessionDescriptor ToSessionDescriptor(
        KeycloakSessionRepresentation session, string userId, string? currentSessionId) =>
        new(
            SessionId: session.Id,
            UserId: userId,
            IsCurrent: string.Equals(session.Id, currentSessionId, StringComparison.Ordinal),
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(session.Start),
            LastAccessedAt: DateTimeOffset.FromUnixTimeMilliseconds(session.LastAccess),
            UserAgent: null,
            IpAddress: session.IpAddress,
            Location: null);

    private static UserDevice ToUserDevice(KeycloakDeviceRepresentation device) =>
        new(
            // The account devices endpoint carries no stable device id, so synthesize a signature
            // from the OS/browser pair (falling back to the IP when both are absent).
            DeviceId: $"{device.Os}/{device.Browser}",
            Kind: DeviceKind.Browser,
            OperatingSystem: device.Os,
            Browser: device.Browser,
            LastSeen: DateTimeOffset.FromUnixTimeMilliseconds(device.LastAccess),
            SessionCount: device.Sessions?.Count ?? 0,
            LastLocation: null);

    private async Task<KeycloakRoleRepresentation> GetRoleByNameAsync(
        HttpClient client, string roleName, CancellationToken cancellationToken)
    {
        string endpoint = options.Value.GetRoleByNameEndpoint(roleName);

        return await client
            .GetFromJsonAsync<KeycloakRoleRepresentation>(endpoint, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Role '{roleName}' not found in Keycloak.");
    }

    // ──── Credential verification ────

    /// <inheritdoc/>
    public async Task<bool> VerifyUserCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.VerifyUserCredentials);

        KeycloakAdminOptions opts = options.Value;

        if (string.IsNullOrEmpty(opts.DirectAccessClientId))
        {
            throw new InvalidOperationException(
                $"{nameof(KeycloakAdminOptions)}.{nameof(KeycloakAdminOptions.DirectAccessClientId)} " +
                "must be configured to use credential verification.");
        }

        HttpClient client = httpClientFactory.CreateClient("KeycloakAdmin");

        using FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", opts.DirectAccessClientId),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
        ]);

        using HttpResponseMessage response = await client
            .PostAsync(opts.GetTokenEndpoint(), content, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            LogCredentialVerificationSucceeded(LogRedaction.Username(username));
            return true;
        }

        LogCredentialVerificationFailed(LogRedaction.Username(username), (int)response.StatusCode);
        return false;
    }

    private static IdentityGroup ToIdentityGroup(KeycloakGroupRepresentation group) =>
        new(
            Id: group.Id,
            Name: group.Name,
            Path: group.Path,
            SubGroups: group.SubGroups?.ConvertAll(ToIdentityGroup) ?? []);

    // -- Source-generated log messages --

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get users from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetUsersFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get user {UserId} from Keycloak. Returning null")]
    private partial void LogKeycloakGetUserFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} {Action} in Keycloak")]
    private partial void LogUserEnabledChanged(string userId, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} profile updated in Keycloak")]
    private partial void LogUserProfileUpdated(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get sessions for user {UserId} from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetSessionsFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get device activity for user {UserId} from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetDeviceActivityFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not read the device kind for Keycloak client {ClientUuid}; defaulting to Browser.")]
    private partial void LogKeycloakClientDeviceKindUnresolved(Exception exception, string clientUuid);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get credentials for user {UserId} from Keycloak. Returning null")]
    private partial void LogKeycloakGetCredentialsFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get roles from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetRolesFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get members of role {RoleName} from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetRoleMembersFailed(Exception exception, string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get roles for user {UserId} from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetUserRolesFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get clients from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetClientsFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get client roles for client {ClientId} from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetClientRolesFailed(Exception exception, string clientId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get client role mappings for user {UserId} on client {ClientId} from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetUserClientRolesFailed(Exception exception, string userId, string clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} assigned to user {UserId} in Keycloak")]
    private partial void LogRoleAssigned(string roleName, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} removed from user {UserId} in Keycloak")]
    private partial void LogRoleRemoved(string roleName, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session {SessionId} terminated for user {UserId} in Keycloak")]
    private partial void LogSessionTerminated(string sessionId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{RevokedCount} other sessions terminated for user {UserId} in Keycloak")]
    private partial void LogOtherSessionsTerminated(string userId, int revokedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset email sent for user {UserId} via Keycloak")]
    private partial void LogPasswordResetEmailSent(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Temporary password set for user {UserId} in Keycloak")]
    private partial void LogTemporaryPasswordSet(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {RedactedUsername} created with ID {UserId} in Keycloak")]
    private partial void LogUserCreated(string redactedUsername, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get groups from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetGroupsFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get groups for user {UserId} from Keycloak. Returning empty list")]
    private partial void LogKeycloakGetUserGroupsFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} added to group {GroupId} in Keycloak")]
    private partial void LogUserAddedToGroup(string userId, string groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} removed from group {GroupId} in Keycloak")]
    private partial void LogUserRemovedFromGroup(string userId, string groupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Credential verification succeeded for user {RedactedUsername}")]
    private partial void LogCredentialVerificationSucceeded(string redactedUsername);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Credential verification failed for user {RedactedUsername} (HTTP {StatusCode})")]
    private partial void LogCredentialVerificationFailed(string redactedUsername, int statusCode);
}
