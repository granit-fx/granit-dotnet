using Granit.Authentication.ApiKeys.Domain;
using Granit.Users;

namespace Granit.Authentication.ApiKeys.Internal;

/// <summary>
/// <see cref="ICurrentUserService"/> implementation for API key authentication contexts.
/// Sets <see cref="ICurrentUserService.ActorKind"/> to
/// <see cref="Security.ActorKind.ExternalSystem"/>.
/// </summary>
internal sealed class ApiKeyCurrentUserService : ICurrentUserService
{
    private readonly ApiKeyEntry _apiKey;

    internal ApiKeyCurrentUserService(ApiKeyEntry apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        _apiKey = apiKey;
    }

    /// <inheritdoc/>
    public string? UserId => _apiKey.Id.ToString();

    /// <inheritdoc/>
    public string? UserName => _apiKey.Name;

    /// <inheritdoc/>
    public string? Email => null;

    /// <inheritdoc/>
    public string? FirstName => null;

    /// <inheritdoc/>
    public string? LastName => null;

    /// <inheritdoc/>
    public bool IsAuthenticated => true;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetRoles() => [];

    /// <inheritdoc/>
    public bool IsInRole(string role) => false;

    /// <inheritdoc/>
    public ActorKind ActorKind => ActorKind.ExternalSystem;

    /// <inheritdoc/>
    public bool IsMachine => true;

    /// <inheritdoc/>
    public Guid? ApiKeyId => _apiKey.Id;
}
