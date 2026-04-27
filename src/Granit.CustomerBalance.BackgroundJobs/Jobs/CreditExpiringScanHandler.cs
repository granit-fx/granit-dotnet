namespace Granit.CustomerBalance.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="CreditExpiringScanJob"/>. Delegates to
/// <see cref="ICreditExpiringScanService"/> for the proactive
/// "credit expiring soon" scan that emits one <c>CreditExpiringEto</c> per matching credit.
/// </summary>
public class CreditExpiringScanHandler
{
    public static async Task HandleAsync(
        CreditExpiringScanJob _,
        ICreditExpiringScanService creditExpiringScanService,
        CancellationToken cancellationToken) =>
        await creditExpiringScanService.ScanAsync(cancellationToken).ConfigureAwait(false);
}
