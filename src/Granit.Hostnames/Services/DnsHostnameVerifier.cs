using DnsClient;
using DnsClient.Protocol;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Hostnames.Services;

/// <summary>
/// Default <see cref="IHostnameVerifier"/> implementation. Resolves each
/// <see cref="ManagedHostname.ExpectedDnsRecords"/> entry against live DNS and
/// maps failures to typed <see cref="DnsConflict"/> instances.
/// Replace this by registering a custom <see cref="IHostnameVerifier"/> before
/// calling <c>AddGranitHostnames</c>.
/// </summary>
internal sealed partial class DnsHostnameVerifier(
    ILookupClient dns,
    ILogger<DnsHostnameVerifier> logger) : IHostnameVerifier
{
    public async Task<HostnameVerificationResult> VerifyAsync(
        ManagedHostname hostname,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hostname);

        if (hostname.ExpectedDnsRecords.Count == 0)
        {
            return new HostnameVerificationResult(IsVerified: false,
                [new DnsConflict(DnsConflictType.ResolutionFailure, "No expected DNS records are configured.")]);
        }

        var conflicts = new List<DnsConflict>();

        foreach (ExpectedDnsRecord record in hostname.ExpectedDnsRecords)
        {
            await CheckRecordAsync(record, conflicts, cancellationToken).ConfigureAwait(false);
        }

        return new HostnameVerificationResult(conflicts.Count == 0, conflicts);
    }

    private async Task CheckRecordAsync(
        ExpectedDnsRecord record,
        List<DnsConflict> conflicts,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (record.RecordType)
            {
                case DnsRecordType.Cname:
                    await CheckCnameAsync(record, conflicts, cancellationToken).ConfigureAwait(false);
                    break;
                case DnsRecordType.Txt:
                    await CheckTxtAsync(record, conflicts, cancellationToken).ConfigureAwait(false);
                    break;
                case DnsRecordType.A:
                    await CheckAAsync(record, conflicts, cancellationToken).ConfigureAwait(false);
                    break;
                case DnsRecordType.Aaaa:
                    await CheckAaaaAsync(record, conflicts, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        catch (DnsResponseException ex)
        {
            LogResolutionFailure(record.Name, ex);
            conflicts.Add(new DnsConflict(
                DnsConflictType.ResolutionFailure,
                $"DNS resolution failed for '{record.Name}': {ex.Code}"));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogResolutionFailure(record.Name, ex);
            conflicts.Add(new DnsConflict(
                DnsConflictType.ResolutionFailure,
                $"Unexpected error resolving '{record.Name}'."));
        }
    }

    private async Task CheckCnameAsync(
        ExpectedDnsRecord record,
        List<DnsConflict> conflicts,
        CancellationToken cancellationToken)
    {
        IDnsQueryResponse response = await dns
            .QueryAsync(record.Name, QueryType.CNAME, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        CNameRecord? cname = response.Answers.CnameRecords().FirstOrDefault();

        if (cname is null)
        {
            conflicts.Add(new DnsConflict(
                DnsConflictType.MissingCname,
                $"No CNAME found for '{record.Name}'."));
            return;
        }

        string actual = cname.CanonicalName.Value.TrimEnd('.');
        string expected = record.Value.TrimEnd('.');

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            conflicts.Add(new DnsConflict(
                DnsConflictType.DivergentCname,
                $"CNAME '{record.Name}' points to '{actual}', expected '{expected}'."));
        }
    }

    private async Task CheckTxtAsync(
        ExpectedDnsRecord record,
        List<DnsConflict> conflicts,
        CancellationToken cancellationToken)
    {
        IDnsQueryResponse response = await dns
            .QueryAsync(record.Name, QueryType.TXT, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        bool found = response.Answers.TxtRecords()
            .SelectMany(r => r.Text)
            .Any(t => string.Equals(t, record.Value, StringComparison.Ordinal));

        if (!found)
        {
            conflicts.Add(new DnsConflict(
                DnsConflictType.MissingTxt,
                $"TXT record '{record.Name}' does not contain the expected value."));
        }
    }

    private async Task CheckAAsync(
        ExpectedDnsRecord record,
        List<DnsConflict> conflicts,
        CancellationToken cancellationToken)
    {
        IDnsQueryResponse response = await dns
            .QueryAsync(record.Name, QueryType.A, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        bool found = response.Answers.ARecords()
            .Any(r => string.Equals(r.Address.ToString(), record.Value, StringComparison.OrdinalIgnoreCase));

        if (!found)
        {
            conflicts.Add(new DnsConflict(
                DnsConflictType.UnexpectedA,
                $"A record for '{record.Name}' does not contain expected address '{record.Value}'."));
        }
    }

    private async Task CheckAaaaAsync(
        ExpectedDnsRecord record,
        List<DnsConflict> conflicts,
        CancellationToken cancellationToken)
    {
        IDnsQueryResponse response = await dns
            .QueryAsync(record.Name, QueryType.AAAA, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        bool found = response.Answers.AaaaRecords()
            .Any(r => string.Equals(r.Address.ToString(), record.Value, StringComparison.OrdinalIgnoreCase));

        if (!found)
        {
            conflicts.Add(new DnsConflict(
                DnsConflictType.UnexpectedAaaa,
                $"AAAA record for '{record.Name}' does not contain expected address '{record.Value}'."));
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DNS resolution error for '{Name}'.")]
    private partial void LogResolutionFailure(string name, Exception ex);
}
