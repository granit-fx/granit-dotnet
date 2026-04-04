using System.Text.Json;
using Granit.Domain;

namespace Granit.Notifications.Messages;

/// <summary>
/// Wolverine message published by <see cref="Abstractions.INotificationPublisher"/> to trigger notification fan-out.
/// </summary>
public sealed record NotificationTrigger
{
#pragma warning disable GRSEC002
    public Guid NotificationId { get; init; } = Guid.NewGuid();
#pragma warning restore GRSEC002
    public required string NotificationTypeName { get; init; }
    public required NotificationSeverity Severity { get; init; }
    public required JsonElement Data { get; init; }
    public IReadOnlyList<string> RecipientUserIds { get; init; } = [];
    public EntityReference? RelatedEntity { get; init; }
    public Guid? TenantId { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? Culture { get; init; }

    /// <summary>
    /// When set, contains the AES-256-CBC encrypted JSON payload of <see cref="Data"/>.
    /// Decrypted by <c>NotificationFanoutHandler</c> before delivery.
    /// Falls back to unencrypted <see cref="Data"/> when encryption is not configured.
    /// </summary>
    public string? EncryptedData { get; init; }

    /// <summary>
    /// When set, bypasses <c>IRecipientResolver</c> and uses this contact info directly.
    /// Used for sending to addresses not yet in the identity store (email change, invitations).
    /// </summary>
    public RecipientInfo? RecipientOverride { get; init; }
}
