namespace Granit.CustomerBalance.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="CreditExpirationScanJob"/>. Delegates to
/// <see cref="ICreditExpirationService"/> for expired promotional credit processing.
/// </summary>
internal static class CreditExpirationScanHandler
{
    public static async Task HandleAsync(
        CreditExpirationScanJob _,
        ICreditExpirationService creditExpirationService,
        CancellationToken cancellationToken) =>
        await creditExpirationService.ExpireCreditsAsync(cancellationToken).ConfigureAwait(false);
}
