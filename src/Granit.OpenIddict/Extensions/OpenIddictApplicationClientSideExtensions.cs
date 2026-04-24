using System.Text.Json;
using Granit.MultiTenancy;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Extensions;

/// <summary>
/// Read/write helpers for the <see cref="MultiTenancySide"/> policy persisted on an
/// OpenIddict application via its <c>Properties</c> dictionary. Used by seeding and
/// by the OIDC server pipeline to enforce host/tenant isolation at sign-in.
/// </summary>
/// <remarks>
/// <para>
/// The policy rides alongside the application record in the OpenIddict
/// <c>Properties</c> bag (an <see cref="IDictionary{TKey,TValue}"/> of
/// <see cref="JsonElement"/>). Storing it on the app record — rather than querying
/// the BFF frontend configuration at authentication time — keeps the OIDC server
/// self-sufficient: a deployment without <c>Granit.Bff</c> can still declare
/// host-only or tenant-only clients, and the host-application author controls the
/// policy through normal seeding configuration.
/// </para>
/// <para>
/// The property value is the invariant enum name (<c>"Host"</c>, <c>"Tenant"</c>,
/// <c>"Both"</c>). Unknown values are treated as <see langword="null"/> (no policy)
/// to avoid locking out every user when a property written by a newer version is
/// read by an older binary.
/// </para>
/// </remarks>
public static class OpenIddictApplicationClientSideExtensions
{
    /// <summary>
    /// Fully-qualified <c>Properties</c> key under which the client-side policy
    /// is stored. The <c>urn:granit:</c> prefix avoids collisions with OpenIddict's
    /// own keys and any other extension.
    /// </summary>
    public const string ClientSidePropertyKey = "urn:granit:openiddict:client_side";

    /// <summary>
    /// Writes the <paramref name="clientSide"/> policy onto the descriptor's
    /// <see cref="OpenIddictApplicationDescriptor.Properties"/>. Passing
    /// <see langword="null"/> removes the key, returning the application to the
    /// "no restriction" default.
    /// </summary>
    public static void SetClientSide(
        this OpenIddictApplicationDescriptor descriptor,
        MultiTenancySide? clientSide)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (clientSide is null)
        {
            descriptor.Properties.Remove(ClientSidePropertyKey);
            return;
        }

        descriptor.Properties[ClientSidePropertyKey] = JsonSerializer.SerializeToElement(
            clientSide.Value.ToString());
    }

    /// <summary>
    /// Reads the client-side policy from the descriptor's
    /// <see cref="OpenIddictApplicationDescriptor.Properties"/>, or
    /// <see langword="null"/> when the key is absent or its value is not a recognised
    /// <see cref="MultiTenancySide"/> member.
    /// </summary>
    public static MultiTenancySide? GetClientSide(this OpenIddictApplicationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!descriptor.Properties.TryGetValue(ClientSidePropertyKey, out JsonElement element))
        {
            return null;
        }

        return ParseClientSide(element);
    }

    /// <summary>
    /// Reads the client-side policy from an application record via the OpenIddict
    /// application manager. Returns <see langword="null"/> when the key is absent
    /// or its value is not a recognised <see cref="MultiTenancySide"/> member.
    /// </summary>
    public static async ValueTask<MultiTenancySide?> GetClientSideAsync(
        this IOpenIddictApplicationManager applicationManager,
        object application,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationManager);
        ArgumentNullException.ThrowIfNull(application);

        System.Collections.Immutable.ImmutableDictionary<string, JsonElement> properties =
            await applicationManager.GetPropertiesAsync(application, cancellationToken)
                .ConfigureAwait(false);

        return properties.TryGetValue(ClientSidePropertyKey, out JsonElement element)
            ? ParseClientSide(element)
            : null;
    }

    private static MultiTenancySide? ParseClientSide(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? raw = element.GetString();
        return Enum.TryParse(raw, ignoreCase: false, out MultiTenancySide value)
            && Enum.IsDefined(value)
            ? value
            : null;
    }
}
