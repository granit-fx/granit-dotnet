using System.Runtime.CompilerServices;
using Granit.MultiTenancy;
using Granit.Notifications.MobilePush.Domain;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.Notifications.MobilePush.Privacy.DataExport;

/// <summary>
/// GDPR Art. 15 export provider for mobile push device tokens — they identify the data
/// subject's devices and must appear in the export like any other personal data.
/// </summary>
/// <remarks>
/// The token value is exported <b>masked</b> (last 4 characters): the plaintext is a
/// sendable push credential, and an export archive must never become a way to exfiltrate
/// it — the same posture as the token management endpoint's list response.
/// </remarks>
public sealed class MobilePushPrivacyDataProvider(
    IMobilePushTokenReader tokenReader,
    ICurrentTenant currentTenant,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "notifications-mobile-push";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.MobilePush";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<MobilePushToken> tokens = await GetTokensAsync(context, cancellationToken).ConfigureAwait(false);
        return tokens.Count > 0;
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
        IReadOnlyList<MobilePushToken> tokens = await GetTokensAsync(context, cancellationToken).ConfigureAwait(false);
        if (tokens.Count == 0)
        {
            yield break;
        }

        var fragment = new MobilePushExportFragment(
            UserId: context.SubjectUserId,
            Tokens: tokens.Select(t => new MobilePushTokenFragment(
                MaskToken(t.DeviceToken), t.Platform.ToString(), t.CreatedAt)).ToList());

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "mobile-push-tokens.json", fragment, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<IReadOnlyList<MobilePushToken>> GetTokensAsync(
        PrivacyExportContext context, CancellationToken cancellationToken) =>
        tokenReader.GetTokensAsync(
            context.SubjectUserId.ToString(),
            currentTenant.IsAvailable ? currentTenant.Id : null,
            cancellationToken);

    private static string MaskToken(string deviceToken) =>
        deviceToken.Length <= 4 ? "…" : $"…{deviceToken.AsSpan(deviceToken.Length - 4)}";
}

/// <summary>Wire shape of the mobile push export fragment.</summary>
public sealed record MobilePushExportFragment(Guid UserId, IReadOnlyList<MobilePushTokenFragment> Tokens);

/// <summary>One masked device token in the export.</summary>
public sealed record MobilePushTokenFragment(string DeviceTokenPreview, string Platform, DateTimeOffset CreatedAt);
