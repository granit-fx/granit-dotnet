using Granit.Localization;

namespace Granit.Authorization;

/// <summary>Defines a single permission that can be granted to a role.</summary>
public sealed record PermissionDefinition(string Name, LocalizableString? DisplayName, string GroupName);
