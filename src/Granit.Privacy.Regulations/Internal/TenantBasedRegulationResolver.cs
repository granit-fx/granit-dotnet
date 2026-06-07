using Granit.MultiTenancy;
using Granit.Privacy.Regulations.Jurisdiction;
using Granit.Privacy.Regulations.Options;
using Granit.Privacy.Regulations.Profiles;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Regulations.Internal;

/// <summary>
/// Default <see cref="IPrivacyRegulationResolver"/> implementation.
/// Resolution chain (highest to lowest precedence):
/// 1. Config per-tenant override (regulation code — operator escape hatch).
/// 2. Tenant <c>Jurisdiction</c> (ISO 3166 code) resolved via <see cref="IPrivacyJurisdictionResolver"/>.
///    A single ISO code may map to multiple regulations (e.g. <c>"CH"</c> → CH_NFADP + EU_GDPR).
///    Falls through to step 3 when the code resolves to nothing (e.g. <c>"US"</c>).
/// 3. Default regulation from configuration.
/// </summary>
internal sealed class TenantBasedRegulationResolver(
    IRegulationProfileRegistry registry,
    IOptions<PrivacyRegulationsOptions> options,
    IPrivacyJurisdictionResolver jurisdictionResolver,
    ICurrentTenant? currentTenant = null) : IPrivacyRegulationResolver
{
    public async Task<PrivacyRegulationProfile> ResolveAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PrivacyRegulationProfile> profiles =
            await ResolveAllAsync(cancellationToken).ConfigureAwait(false);

        return CompositeRegulationProfileMerger.Merge(profiles);
    }

    public async Task<IReadOnlyList<PrivacyRegulationProfile>> ResolveAllAsync(CancellationToken cancellationToken = default)
    {
        PrivacyRegulationsOptions opts = options.Value;

        if (currentTenant is { IsAvailable: true, Id: { } tenantId })
        {
            // 1. Config per-tenant override — explicit regulation code, takes precedence over ISO resolution
            if (opts.TenantRegulations.TryGetValue(tenantId.ToString(), out string? configCode)
                && !string.IsNullOrWhiteSpace(configCode))
            {
                return [LookupRequired(configCode)];
            }

            // 2. ISO 3166 jurisdiction code from DB
            string? isoCode = currentTenant.Jurisdiction;
            if (!string.IsNullOrWhiteSpace(isoCode))
            {
                (string country, string? region) = ParseIsoCode(isoCode);
                IReadOnlyList<PrivacyRegulation> resolved =
                    await jurisdictionResolver.ResolveAsync(country, region, cancellationToken)
                        .ConfigureAwait(false);

                if (resolved.Count > 0)
                {
                    return resolved.Select(r => LookupRequired(r.Value)).ToList();
                }

                // ISO code resolved to nothing (e.g. "US" — no federal general privacy law)
                // → fall through to default
            }
        }

        // 3. Default regulation from configuration
        string? defaultCode = opts.DefaultRegulation;

        if (string.IsNullOrWhiteSpace(defaultCode))
        {
            throw new InvalidOperationException(
                "No privacy regulation configured. " +
                "Set 'Privacy:Regulations:DefaultRegulation' in appsettings.json, " +
                "configure per-tenant regulations in 'Privacy:Regulations:TenantRegulations', " +
                "or set an ISO 3166 jurisdiction code on the tenant.");
        }

        return [LookupRequired(defaultCode)];
    }

    private PrivacyRegulationProfile LookupRequired(string regulationCode) =>
        registry.GetProfile(PrivacyRegulation.Create(regulationCode))
            ?? throw new InvalidOperationException(
                $"Privacy regulation '{regulationCode}' is not registered. " +
                $"Ensure a matching IRegulationProfileProvider is loaded. " +
                $"Available regulations: {string.Join(", ", registry.GetAll().Select(p => p.Regulation.Value))}.");

    /// <summary>
    /// Parses an ISO 3166 code into country + optional region.
    /// <c>"FR"</c> → <c>("FR", null)</c>. <c>"CA-QC"</c> → <c>("CA", "CA-QC")</c>.
    /// </summary>
    private static (string Country, string? Region) ParseIsoCode(string code)
    {
        int dash = code.IndexOf('-');
        return dash > 0 ? (code[..dash], code) : (code, null);
    }
}
