using Granit.Mcp.Server.Permissions;

namespace Granit.Mcp.Server.Internal;

/// <summary>
/// Maps OAuth 2.0 scopes to MCP permission names.
/// Used during token validation to translate bearer token scopes
/// (e.g., <c>mcp:tools:read</c>) into Granit permission constants
/// (e.g., <see cref="McpPermissions.Tools.Read"/>).
/// </summary>
internal static class PermissionScopeMappingService
{
    /// <summary>
    /// Scope separator in OAuth tokens. Scopes use colons (<c>mcp:tools:read</c>)
    /// while Granit permissions use dots (<c>Mcp.Tools.Read</c>).
    /// </summary>
    private const char ScopeSeparator = ':';
    private const char PermissionSeparator = '.';

    /// <summary>
    /// All known MCP permission constants, keyed by their lowercase scope equivalent.
    /// </summary>
    private static readonly Dictionary<string, string> ScopeToPermission = BuildScopeMap();

    /// <summary>
    /// Translates an OAuth scope string to the corresponding Granit permission name.
    /// </summary>
    /// <param name="scope">The OAuth scope (e.g., <c>mcp:server:access</c>).</param>
    /// <returns>The Granit permission name, or <see langword="null"/> if no mapping exists.</returns>
    public static string? MapScopeToPermission(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        string normalized = scope.ToLowerInvariant().Trim();
        return ScopeToPermission.GetValueOrDefault(normalized);
    }

    /// <summary>
    /// Translates a Granit permission name to the corresponding OAuth scope string.
    /// </summary>
    /// <param name="permission">The Granit permission (e.g., <c>Mcp.Tools.Read</c>).</param>
    /// <returns>The OAuth scope, or <see langword="null"/> if no mapping exists.</returns>
    public static string? MapPermissionToScope(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        string candidate = permission.ToLowerInvariant().Replace(PermissionSeparator, ScopeSeparator);
        return ScopeToPermission.ContainsKey(candidate) ? candidate : null;
    }

    /// <summary>
    /// Returns all known OAuth scopes for MCP permissions.
    /// </summary>
    public static IReadOnlyCollection<string> GetAllScopes() => ScopeToPermission.Keys;

    private static Dictionary<string, string> BuildScopeMap()
    {
        string[] permissions =
        [
            McpPermissions.Server.Access,
            McpPermissions.Tools.Read,
            McpPermissions.Tools.Execute,
            McpPermissions.Resources.Read,
            McpPermissions.Prompts.Read,
        ];

        Dictionary<string, string> map = new(permissions.Length, StringComparer.OrdinalIgnoreCase);
        foreach (string permission in permissions)
        {
            string scope = permission.ToLowerInvariant().Replace(PermissionSeparator, ScopeSeparator);
            map[scope] = permission;
        }

        return map;
    }
}
