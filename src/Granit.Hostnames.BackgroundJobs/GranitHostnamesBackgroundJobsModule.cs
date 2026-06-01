using Granit.BackgroundJobs;
using Granit.Hostnames.BackgroundJobs.Services;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Hostnames.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Hostnames: DNS verification polling.
/// </summary>
/// <remarks>
/// The module ships the <see cref="Jobs.VerifyHostnamesJob"/> + handler shape but does not
/// own the <see cref="Granit.Hostnames.Contracts.IHostnameVerifier"/> implementation — that
/// is provided by <c>Granit.Hostnames.EntityFrameworkCore</c> (built-in DNS) or an adapter
/// package. Hosts must register a verifier implementation before using this module.
/// </remarks>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitHostnamesModule))]
public sealed class GranitHostnamesBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<HostnameVerificationBatchService>();
}
