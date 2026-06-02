using Granit.Guids;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Options;
using Microsoft.Extensions.Options;

namespace Granit.Hostnames.Services;

/// <summary>
/// Default <see cref="IHostnameRegistrationService"/> implementation. Encapsulates the
/// token-minting, expected-record building, and the Pending / Active|Error branching
/// so endpoints and other consumers do not duplicate this logic.
/// </summary>
internal sealed class HostnameRegistrationService(
    IManagedHostnameReader reader,
    IManagedHostnameWriter writer,
    IGuidGenerator guidGenerator,
    IOptions<HostnamesOptions> hostnamesOptions) : IHostnameRegistrationService
{
    public async Task<HostnameRegistrationResult> RegisterAsync(
        string host,
        string ownerType,
        Guid ownerId,
        Guid? tenantId = null,
        bool isPrimary = false,
        CancellationToken cancellationToken = default)
    {
        // ArgumentException from Hostname.Create propagates to the caller for the invalid-FQDN path.
        var hostnameValue = Hostname.Create(host);

        ManagedHostname? existing = await reader
            .FindByHostAsync(hostnameValue.Value, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return new HostnameRegistrationResult(HostnameRegistrationOutcome.HostAlreadyTaken, null);
        }

        var hostname = ManagedHostname.Create(
            guidGenerator.Create(),
            hostnameValue,
            ownerType,
            ownerId,
            tenantId,
            isPrimary);

        HostnamesOptions opts = hostnamesOptions.Value;
        if (!string.IsNullOrEmpty(opts.IngressTarget))
        {
            string token = guidGenerator.Create().ToString("N");
            hostname.BeginVerification(token, BuildExpectedRecords(hostnameValue.Value, token, opts));
        }

        await writer.AddAsync(hostname, cancellationToken).ConfigureAwait(false);
        return new HostnameRegistrationResult(HostnameRegistrationOutcome.Succeeded, hostname);
    }

    public async Task<RequestVerificationResult> RequestVerificationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ManagedHostname? hostname = await reader
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);

        if (hostname is null)
        {
            return new RequestVerificationResult(RequestVerificationOutcome.NotFound, null);
        }

        if (hostname.Status == HostnameStatus.Pending)
        {
            HostnamesOptions opts = hostnamesOptions.Value;
            if (string.IsNullOrEmpty(opts.IngressTarget))
            {
                return new RequestVerificationResult(RequestVerificationOutcome.IngressNotConfigured, null);
            }

            string token = guidGenerator.Create().ToString("N");
            hostname.BeginVerification(token, BuildExpectedRecords(hostname.Host.Value, token, opts));
        }
        else
        {
            hostname.RequestRecheck();
        }

        await writer.UpdateAsync(hostname, cancellationToken).ConfigureAwait(false);
        return new RequestVerificationResult(RequestVerificationOutcome.Succeeded, hostname);
    }

    private static IReadOnlyList<ExpectedDnsRecord> BuildExpectedRecords(
        string host, string token, HostnamesOptions opts) =>
    [
        new(DnsRecordType.Cname, host, opts.IngressTarget!),
        new(DnsRecordType.Txt, $"{opts.TxtChallengePrefix}.{host}", $"granit-verify={token}"),
    ];
}
