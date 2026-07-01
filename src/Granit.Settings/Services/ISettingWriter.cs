namespace Granit.Settings.Services;

/// <summary>
/// Setting write service for the Global, Tenant, and User scopes.
/// </summary>
public interface ISettingWriter
{
    /// <summary>Sets the value of a setting at the global level.</summary>
    Task SetGlobalAsync(string name, string? value, CancellationToken cancellationToken = default);

    /// <summary>Sets the value of a setting for a specific tenant.</summary>
    Task SetForTenantAsync(Guid tenantId, string name, string? value, CancellationToken cancellationToken = default);

    /// <summary>Sets the value of a setting for a specific user.</summary>
    Task SetForUserAsync(string userId, string name, string? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the value of a setting for a given provider and key.
    /// </summary>
    /// <param name="name">Setting name.</param>
    /// <param name="providerName">Provider name ("G", "T", "U").</param>
    /// <param name="providerKey">Provider key (null = global, tenantId, userId).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        string name,
        string providerName,
        string? providerKey = null,
        CancellationToken cancellationToken = default);
}
