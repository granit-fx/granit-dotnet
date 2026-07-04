namespace Granit.Webhooks.Domain;

/// <summary>
/// Lifecycle status of a <see cref="WebhookSigningKey"/>.
/// </summary>
/// <remarks>
/// <para>
/// Verification rules per status:
/// </para>
/// <list type="bullet">
///   <item><see cref="Active"/>: used to sign new deliveries. Always accepted in verification.</item>
///   <item><see cref="Retired"/>: previously active, kept around during the rotation grace
///     period. Still accepted in verification while <see cref="WebhookSigningKey.ExpiresAt"/>
///     is in the future.</item>
///   <item><see cref="Revoked"/>: explicitly invalidated by an operator. Never accepted.</item>
/// </list>
/// </remarks>
public enum WebhookSigningKeyStatus
{
    /// <summary>Currently used to sign outgoing deliveries.</summary>
    Active,

    /// <summary>
    /// Replaced by a newer <see cref="Active"/> key but still accepted during the
    /// rotation grace period (controlled by <see cref="WebhookSigningKey.ExpiresAt"/>).
    /// </summary>
    Retired,

    /// <summary>Explicitly invalidated by an operator. Never accepted.</summary>
    Revoked,
}
