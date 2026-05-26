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
    public void SignBytes_then_VerifyBytes_returns_true()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] payload = Encoding.UTF8.GetBytes("""{"schemaVersion":1,"shards":[]}""");

        string tag = signer.SignBytes(payload);
        signer.VerifyBytes(payload, tag).ShouldBeTrue();
    }

    [Fact]
    public void VerifyBytes_returns_false_when_payload_tampered_with()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] original = Encoding.UTF8.GetBytes("""{"shardCount":3}""");
        string tag = signer.SignBytes(original);

        byte[] tampered = Encoding.UTF8.GetBytes("""{"shardCount":4}""");
        signer.VerifyBytes(tampered, tag).ShouldBeFalse();
    }

    [Fact]
    public void VerifyBytes_returns_false_for_unknown_version()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] payload = Encoding.UTF8.GetBytes("anything");
        string realTag = signer.SignBytes(payload);

        // Swap version prefix to v99 — even if the MAC happens to be valid for v1,
        // the version mismatch must reject.
        string forged = "v99" + realTag[realTag.IndexOf(':')..];
        signer.VerifyBytes(payload, forged).ShouldBeFalse();
    }

    [Fact]
    public void VerifyBytes_returns_false_on_malformed_tag()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        byte[] payload = Encoding.UTF8.GetBytes("data");

        signer.VerifyBytes(payload, "no-colon").ShouldBeFalse();
        signer.VerifyBytes(payload, "v1:%%%bad-base64%%%").ShouldBeFalse();
    }

    [Fact]
    public void FragmentTag_and_ContentTag_dont_cross_verify()
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

        string fragmentTag = signer.Sign(fragmentParams);
        byte[] fragmentCanonicalBytes = Encoding.UTF8.GetBytes(fragmentParams.RequestId.ToString());

        // Fragment tag should NOT verify when treated as a content tag over arbitrary bytes.
        signer.VerifyBytes(fragmentCanonicalBytes, fragmentTag).ShouldBeFalse();
    }
}
