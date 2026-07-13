using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Vault.Options;
using Granit.Vault;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.Vault;

/// <summary>
/// Vault-backed implementation of <see cref="IExportHmacSigner"/> and
/// <see cref="IExportContentSigner"/>. Delegates the actual MAC operation to
/// whichever <see cref="ITransitMacService"/> the host registered (HashiCorp,
/// AWS KMS, GCP KMS, Azure Managed HSM, or the portable secret-backed fallback).
/// </summary>
/// <remarks>
/// <para>
/// The signer contracts are async-first, so the underlying
/// <see cref="ITransitMacService"/> calls are awaited directly — no
/// sync-over-async bridge.
/// </para>
/// <para>
/// <b>Tag format:</b> <c>gpv1:{providerOpaqueTag}</c>. The <c>gpv1</c> prefix lets
/// downstream code reject legacy <see cref="EphemeralExportHmacSigner"/> tags
/// during cutover — a host that swaps signers gets a clean failure for in-flight
/// exports rather than silent corruption.
/// </para>
/// </remarks>
public sealed partial class VaultExportHmacSigner : IExportHmacSigner, IExportContentSigner
{
    internal const string TagPrefix = "gpv1:";

    private readonly ITransitMacService _macService;
    private readonly VaultExportSignerOptions _options;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance.</summary>
    public VaultExportHmacSigner(
        ITransitMacService macService,
        IOptions<VaultExportSignerOptions> options,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(macService);
        ArgumentNullException.ThrowIfNull(options);

        _macService = macService;
        _options = options.Value;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<string> SignAsync(ExportHmacParameters parameters, CancellationToken cancellationToken = default)
    {
        byte[] canonical = ExportHmacCanonicalizer.Canonicalize(parameters);
        TransitMacResult result = await _macService
            .MacAsync(_options.FragmentKeyName, canonical, cancellationToken)
            .ConfigureAwait(false);
        return $"{TagPrefix}{result.Mac}";
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(ExportHmacParameters parameters, string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (parameters.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return false;
        }

        string providerTag = tag[TagPrefix.Length..];
        byte[] canonical = ExportHmacCanonicalizer.Canonicalize(parameters);
        return await _macService
            .VerifyAsync(_options.FragmentKeyName, canonical, providerTag, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string> SignBytesAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        TransitMacResult result = await _macService
            .MacAsync(_options.ContentKeyName, payload.ToArray(), cancellationToken)
            .ConfigureAwait(false);
        return $"{TagPrefix}{result.Mac}";
    }

    /// <inheritdoc />
    public async Task<bool> VerifyBytesAsync(ReadOnlyMemory<byte> payload, string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string providerTag = tag[TagPrefix.Length..];
        return await _macService
            .VerifyAsync(_options.ContentKeyName, payload.ToArray(), providerTag, cancellationToken)
            .ConfigureAwait(false);
    }
}
