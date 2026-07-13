using System.Runtime.CompilerServices;
using Granit.MultiTenancy;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.Notifications.WebPush.Privacy.DataExport;

/// <summary>
/// GDPR Art. 15 export provider for browser Web Push subscriptions — the endpoint URL
/// identifies the data subject's browser and must appear in the export.
/// </summary>
/// <remarks>
/// The endpoint is exported <b>masked</b> (origin only): the full URL is an unguessable
/// capability credential minted by the browser's push service, and the encryption key
/// material (P256dh/Auth) is never exported at all.
/// </remarks>
public sealed class WebPushPrivacyDataProvider(
    IWebPushSubscriptionReader subscriptionReader,
    ICurrentTenant currentTenant,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "notifications-web-push";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.WebPush";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<WebPushSubscriptionInfo> subscriptions =
            await GetSubscriptionsAsync(context, cancellationToken).ConfigureAwait(false);
        return subscriptions.Count > 0;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExportFragment> ExportAsync(
        PrivacyExportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExportCoreAsync(context, cancellationToken);
    }

    private async IAsyncEnumerable<ExportFragment> ExportCoreAsync(
        PrivacyExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<WebPushSubscriptionInfo> subscriptions =
            await GetSubscriptionsAsync(context, cancellationToken).ConfigureAwait(false);
        if (subscriptions.Count == 0)
        {
            yield break;
        }

        var fragment = new WebPushExportFragment(
            UserId: context.SubjectUserId,
            Subscriptions: subscriptions.Select(s => new WebPushSubscriptionFragment(
                MaskEndpoint(s.Endpoint), s.ExpirationTime)).ToList());

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "web-push-subscriptions.json", fragment, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<IReadOnlyList<WebPushSubscriptionInfo>> GetSubscriptionsAsync(
        PrivacyExportContext context, CancellationToken cancellationToken) =>
        subscriptionReader.GetSubscriptionsAsync(
            context.SubjectUserId.ToString(),
            currentTenant.IsAvailable ? currentTenant.Id : null,
            cancellationToken);

    /// <summary>Origin only — the path is the unguessable capability part.</summary>
    internal static string MaskEndpoint(string endpoint) =>
        Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
            ? $"{uri.Scheme}://{uri.Host}/…"
            : "…";
}

/// <summary>Wire shape of the Web Push export fragment.</summary>
public sealed record WebPushExportFragment(Guid UserId, IReadOnlyList<WebPushSubscriptionFragment> Subscriptions);

/// <summary>One masked subscription in the export — key material is never exported.</summary>
public sealed record WebPushSubscriptionFragment(string EndpointOrigin, long? ExpirationTime);
