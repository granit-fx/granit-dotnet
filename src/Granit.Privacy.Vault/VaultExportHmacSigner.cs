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
/// <b>Why Sync interfaces over an async primitive:</b> the privacy export saga
/// signs every fragment inside a Wolverine handler that already runs on the
/// async path — but the <see cref="IExportHmacSigner"/> contract is synchronous
/// because the assembler verifies in tight loops where the surrounding I/O is
/// blocking (FileStream reads). The sync→async bridge uses
/// <c>GetAwaiter().GetResult()</c>; the call is rare (one per fragment, on the
/// order of seconds between calls) and avoids cascading the interface change
/// through every existing caller.
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

    /// <summary>Initializes a new instance.</summary>
    public VaultExportHmacSigner(
        ITransitMacService macService,
        IOptions<VaultExportSignerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(macService);
        ArgumentNullException.ThrowIfNull(options);

        _macService = macService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Sign(in ExportHmacParameters parameters)
    {
        byte[] canonical = ExportHmacCanonicalizer.Canonicalize(parameters);
        TransitMacResult result = _macService
            .MacAsync(_options.FragmentKeyName, canonical, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return $"{TagPrefix}{result.Mac}";
    }

    /// <inheritdoc />
    public bool Verify(in ExportHmacParameters parameters, string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (parameters.ExpiresAt <= TimeProvider.System.GetUtcNow())
        {
            return false;
        }

        string providerTag = tag[TagPrefix.Length..];
        byte[] canonical = ExportHmacCanonicalizer.Canonicalize(parameters);
        return _macService
            .VerifyAsync(_options.FragmentKeyName, canonical, providerTag, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    /// <inheritdoc />
    public string SignBytes(ReadOnlySpan<byte> payload)
    {
        // Copy span to heap — ITransitMacService is async and span is stack-only.
        byte[] heap = payload.ToArray();
        TransitMacResult result = _macService
            .MacAsync(_options.ContentKeyName, heap, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return $"{TagPrefix}{result.Mac}";
    }

    /// <inheritdoc />
    public bool VerifyBytes(ReadOnlySpan<byte> payload, string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        if (!tag.StartsWith(TagPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string providerTag = tag[TagPrefix.Length..];
        byte[] heap = payload.ToArray();
        return _macService
            .VerifyAsync(_options.ContentKeyName, heap, providerTag, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }
}
