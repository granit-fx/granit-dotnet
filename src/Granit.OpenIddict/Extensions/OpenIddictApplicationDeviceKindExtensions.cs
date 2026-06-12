using System.Text.Json;
using Granit.UserSessions;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Extensions;

/// <summary>
/// Read/write helpers for the <see cref="DeviceKind"/> an OpenIddict application declares for the devices
/// that authenticate through it, persisted on the application's <c>Properties</c> dictionary. Read by the
/// session adapters so <c>/devices</c> shows an accurate, trustworthy classification.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DeviceKind"/> cannot be inferred reliably from the User-Agent — it follows from the
/// authentication context (this declaration, with a redirect-URI/grant heuristic as fallback). Declaring it on
/// the application record — rather than guessing per request — makes the classification deterministic and lets
/// the host author own it through normal seeding, exactly like
/// <see cref="OpenIddictApplicationClientSideExtensions"/> does for the client-side policy.
/// </para>
/// <para>
/// The value is stored as the invariant enum name (<c>"MobileApp"</c>, <c>"Tv"</c>, …) under a
/// <c>urn:granit:</c>-prefixed key. <see cref="DeviceKind.Unknown"/> and unrecognised values both read back as
/// <see langword="null"/> ("not declared"), so a kind written by a newer binary never mis-resolves on an older
/// one — the consumer falls back to its heuristic instead.
/// </para>
/// </remarks>
public static class OpenIddictApplicationDeviceKindExtensions
{
    /// <summary>
    /// Fully-qualified <c>Properties</c> key under which the declared device kind is stored. The
    /// <c>urn:granit:</c> prefix avoids collisions with OpenIddict's own keys and any other extension.
    /// </summary>
    public const string DeviceKindPropertyKey = "urn:granit:openiddict:device_kind";

    /// <summary>
    /// Writes the declared <paramref name="deviceKind"/> onto the descriptor's
    /// <see cref="OpenIddictApplicationDescriptor.Properties"/>. Passing <see langword="null"/> or
    /// <see cref="DeviceKind.Unknown"/> removes the key, returning the application to "not declared".
    /// </summary>
    public static void SetDeviceKind(
        this OpenIddictApplicationDescriptor descriptor,
        DeviceKind? deviceKind)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (deviceKind is null or DeviceKind.Unknown)
        {
            descriptor.Properties.Remove(DeviceKindPropertyKey);
            return;
        }

        descriptor.Properties[DeviceKindPropertyKey] = JsonSerializer.SerializeToElement(
            deviceKind.Value.ToString());
    }

    /// <summary>
    /// Reads the declared device kind from the descriptor's
    /// <see cref="OpenIddictApplicationDescriptor.Properties"/>, or <see langword="null"/> when the key is
    /// absent or its value is not a recognised, non-<see cref="DeviceKind.Unknown"/> member.
    /// </summary>
    public static DeviceKind? GetDeviceKind(this OpenIddictApplicationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor.Properties.TryGetValue(DeviceKindPropertyKey, out JsonElement element)
            ? ParseDeviceKind(element)
            : null;
    }

    /// <summary>
    /// Reads the declared device kind from an application record via the OpenIddict application manager.
    /// Returns <see langword="null"/> when the key is absent or its value is not a recognised,
    /// non-<see cref="DeviceKind.Unknown"/> member.
    /// </summary>
    public static async ValueTask<DeviceKind?> GetDeviceKindAsync(
        this IOpenIddictApplicationManager applicationManager,
        object application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationManager);
        ArgumentNullException.ThrowIfNull(application);

        System.Collections.Immutable.ImmutableDictionary<string, JsonElement> properties =
            await applicationManager.GetPropertiesAsync(application, cancellationToken)
                .ConfigureAwait(false);

        return properties.TryGetValue(DeviceKindPropertyKey, out JsonElement element)
            ? ParseDeviceKind(element)
            : null;
    }

    private static DeviceKind? ParseDeviceKind(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? raw = element.GetString();
        return Enum.TryParse(raw, ignoreCase: false, out DeviceKind value)
            && Enum.IsDefined(value)
            && value != DeviceKind.Unknown
            ? value
            : null;
    }
}
