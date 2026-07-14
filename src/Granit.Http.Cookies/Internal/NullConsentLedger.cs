using Granit.Http.Cookies.Domain;
using Granit.Http.Cookies.Ledger;
using Microsoft.Extensions.Logging;

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Default no-op consent ledger: accepts every decision without persisting it.
/// Register <c>Granit.Http.Cookies.EntityFrameworkCore</c> for a durable,
/// GDPR Art. 7(1) accountable ledger.
/// </summary>
/// <remarks>
/// Never silently no-ops: each swallowed decision is logged at Debug level so an
/// operator diagnosing "where are my consent records?" finds the answer in the logs.
/// </remarks>
internal sealed partial class NullConsentLedger(ILogger<NullConsentLedger> logger) : IConsentLedger
{
    /// <inheritdoc/>
    public Task RecordAsync(CookieConsentRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        LogDecisionDiscarded(record.CmpSource, record.Mode);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Cookie-consent decision received (cmp: {CmpSource}, mode: {Mode}) but no persistent ledger is registered — the decision is not recorded. Add Granit.Http.Cookies.EntityFrameworkCore for a durable ledger.")]
    private partial void LogDecisionDiscarded(string cmpSource, CookieConsentMode mode);
}
