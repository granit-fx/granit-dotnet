using System.Text;
using Granit.Privacy.DataExport.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport.Security;

public sealed class EphemeralExportContentSignerTests
{
    private static EphemeralExportHmacSigner CreateSigner() =>
        new(NullLogger<EphemeralExportHmacSigner>.Instance);

    [Fact]
    public async Task SignBytes_then_VerifyBytes_returns_true()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] payload = Encoding.UTF8.GetBytes("""{"schemaVersion":1,"shards":[]}""");

        string tag = await signer.SignBytesAsync(payload, TestContext.Current.CancellationToken);
        (await signer.VerifyBytesAsync(payload, tag, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyBytes_returns_false_when_payload_tampered_with()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] original = Encoding.UTF8.GetBytes("""{"shardCount":3}""");
        string tag = await signer.SignBytesAsync(original, TestContext.Current.CancellationToken);

        byte[] tampered = Encoding.UTF8.GetBytes("""{"shardCount":4}""");
        (await signer.VerifyBytesAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyBytes_returns_false_for_unknown_version()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] payload = Encoding.UTF8.GetBytes("anything");
        string realTag = await signer.SignBytesAsync(payload, TestContext.Current.CancellationToken);

        // Swap version prefix to v99 — even if the MAC happens to be valid for v1,
        // the version mismatch must reject.
        string forged = "v99" + realTag[realTag.IndexOf(':')..];
        (await signer.VerifyBytesAsync(payload, forged, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyBytes_returns_false_on_malformed_tag()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] payload = Encoding.UTF8.GetBytes("data");

        (await signer.VerifyBytesAsync(payload, "no-colon", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await signer.VerifyBytesAsync(payload, "v1:%%%bad-base64%%%", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task FragmentTag_and_ContentTag_dont_cross_verify()
    {
        // Even though both interfaces share the same key, a fragment-identity tag
        // must not validate against a content payload of the same bytes (the
        // canonicalisations are different domains).
        using EphemeralExportHmacSigner signer = CreateSigner();

        ExportHmacParameters fragmentParams = new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: Guid.NewGuid(),
            ProviderName: "identity-local",
            FragmentKind: "staged",
            SourceContainer: "gdpr-exports",
            SourceBlobId: Guid.NewGuid(),
            EntryPath: "identity-local.json",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(30));

        string fragmentTag = await signer.SignAsync(fragmentParams, TestContext.Current.CancellationToken);
        byte[] fragmentCanonicalBytes = Encoding.UTF8.GetBytes(fragmentParams.RequestId.ToString());

        // Fragment tag should NOT verify when treated as a content tag over arbitrary bytes.
        (await signer.VerifyBytesAsync(fragmentCanonicalBytes, fragmentTag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
}
