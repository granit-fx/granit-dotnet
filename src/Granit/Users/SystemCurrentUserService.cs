namespace Granit.Users;

/// <summary>
/// <see cref="ICurrentUserService"/> implementation for internal system processes
/// (background jobs, CRON tasks, migrations, seed operations) that have no HTTP
/// context or external identity.
/// </summary>
/// <remarks>
/// <para>
/// Sets <see cref="ICurrentUserService.ActorKind"/> to <see cref="Users.ActorKind.System"/>
/// and provides <c>"system"</c> as <see cref="UserId"/> so that EF Core audit
/// interceptors record a non-null <c>ModifiedBy</c> value in the ISO 27001 trail.
/// </para>
/// </remarks>
public sealed class SystemCurrentUserService : ICurrentUserService
{
    /// <summary>
    /// The user ID used for system-initiated operations.
    /// </summary>
    public const string SystemUserId = "system";

    /// <inheritdoc/>
    public string? UserId => SystemUserId;

    /// <inheritdoc/>
    public string? UserName => SystemUserId;

    /// <inheritdoc/>
    public string? Email => null;

    /// <inheritdoc/>
    public string? FirstName => null;

    /// <inheritdoc/>
    public string? LastName => null;

    /// <inheritdoc/>
    public bool IsAuthenticated => false;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRoles() => [];

    /// <inheritdoc/>
    public bool IsInRole(string role) => false;

    /// <inheritdoc/>
    public ActorKind ActorKind => ActorKind.System;

    /// <inheritdoc/>
    public bool IsMachine => true;

    /// <inheritdoc/>
    public Guid? ApiKeyId => null;
}
