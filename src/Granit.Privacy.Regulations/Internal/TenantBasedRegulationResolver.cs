using Granit.MultiTenancy;
using Granit.Privacy.Regulations.Options;
using Granit.Privacy.Regulations.Profiles;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Regulations.Internal;

/// <summary>
/// Default <see cref="IPrivacyRegulationResolver"/> implementation.
/// Resolution chain: ICurrentTenant.Jurisdiction (populated from DB by the middleware) →
/// per-tenant config override → default regulation → throw.
/// </summary>
internal sealed class TenantBasedRegulationResolver(
    IRegulationProfileRegistry registry,
    IOptions<PrivacyRegulationsOptions> options,
    ICurrentTenant? currentTenant = null) : IPrivacyRegulationResolver
{
    public Task<PrivacyRegulationProfile> ResolveAsync(CancellationToken cancellationToken = default)
    {
        PrivacyRegulationsOptions opts = options.Value;
        string? regulationCode = null;

        if (currentTenant is { IsAvailable: true, Id: { } tenantId })
        {
            // 1. DB jurisdiction — already resolved by the middleware via FindByIdAsync and
            //    stored on ICurrentTenant when ValidateTenantExistence is enabled.
            regulationCode = currentTenant.Jurisdiction;

            // 2. Config per-tenant override (operator escape hatch, takes precedence over DB)
            if (opts.TenantRegulations.TryGetValue(tenantId.ToString(), out string? configCode))
            {
                regulationCode = configCode;
            }
        }

        // 3. Default regulation from configuration
        regulationCode ??= opts.DefaultRegulation;

        if (string.IsNullOrWhiteSpace(regulationCode))
        {
            throw new InvalidOperationException(
                "No privacy regulation configured. " +
                "Set 'Privacy:Regulations:DefaultRegulation' in appsettings.json, " +
                "configure per-tenant regulations in 'Privacy:Regulations:TenantRegulations', " +
                "or set the Jurisdiction field on the tenant.");
        }

        PrivacyRegulationProfile profile = registry.GetProfile(PrivacyRegulation.Create(regulationCode))
            ?? throw new InvalidOperationException(
                $"Privacy regulation '{regulationCode}' is not registered. " +
                $"Ensure a matching IRegulationProfileProvider is loaded. " +
                $"Available regulations: {string.Join(", ", registry.GetAll().Select(p => p.Regulation.Value))}.");

        return Task.FromResult(profile);
    }

    public async Task<IReadOnlyList<PrivacyRegulationProfile>> ResolveAllAsync(CancellationToken cancellationToken = default)
    {
        // For now, returns only the primary regulation.
        // Composite multi-regulation support is handled via explicit composite profiles.
        PrivacyRegulationProfile primary = await ResolveAsync(cancellationToken).ConfigureAwait(false);
        return [primary];
    }
}
