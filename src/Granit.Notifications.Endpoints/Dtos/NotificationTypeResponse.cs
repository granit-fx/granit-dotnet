namespace Granit.Notifications.Endpoints.Dtos;

/// <summary>
/// Wire shape of a registered notification type — the domain
/// <c>NotificationDefinition</c> never leaks into the OpenAPI contract.
/// </summary>
public sealed record NotificationTypeResponse(
    string Name,
    string? DisplayName,
    string? Description,
    string? GroupName,
    string DefaultSeverity,
    IReadOnlyList<string> DefaultChannels,
    bool AllowUserOptOut);
