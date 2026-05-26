using Granit.Privacy.Diagnostics;

namespace Granit.Privacy.DataExport.Internal;

/// <summary>
/// Default <see cref="IPrivacyScopeResolver"/> — applies the three visibility gates in
/// registration order. Scoped lifetime so it can resolve scoped
/// <see cref="IPrivacyDataProvider"/> instances via <see cref="IServiceProvider"/>.
/// </summary>
internal sealed class PrivacyScopeResolver(
    IDataProviderRegistry registry,
    IPrivacyScopeVisibilityPolicy visibilityPolicy,
    IServiceProvider serviceProvider,
    PrivacyMetrics metrics) : IPrivacyScopeResolver
{
    public async Task<IReadOnlyList<ProviderDescriptor>> ListVisibleAsync(
        PrivacyExportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<ProviderRegistration> registrations = registry.GetAllRegistrations();
        List<ProviderDescriptor> visible = new(registrations.Count);

        foreach (ProviderRegistration registration in registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProviderDescriptor descriptor = new(
                ProviderName: registration.ProviderName,
                DisplayKey: registration.DisplayKey,
                FeatureName: registration.FeatureName);

            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            bool hasData;
            try
            {
                hasData = await registration.HasDataProbe(serviceProvider, context, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                TimeSpan elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp);
                metrics.RecordScopeProbeDuration(
                    context.TenantId, registration.ProviderName, elapsed);
            }

            if (!hasData)
            {
                continue;
            }

            bool allowedByPolicy = await visibilityPolicy
                .IsVisibleAsync(descriptor, context, cancellationToken)
                .ConfigureAwait(false);
            if (!allowedByPolicy)
            {
                continue;
            }

            visible.Add(descriptor);
        }

        return visible;
    }
}
