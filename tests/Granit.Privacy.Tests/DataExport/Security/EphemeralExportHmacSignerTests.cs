using Granit.Privacy.DataExport.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport.Security;

public class EphemeralExportHmacSignerTests
{
    private static EphemeralExportHmacSigner CreateSigner() =>
        new(NullLogger<EphemeralExportHmacSigner>.Instance);

    private static ExportHmacParameters SampleParameters(
        Guid? requestId = null,
        Guid? subjectUserId = null,
        string providerName = "identity-local",
        string fragmentKind = "staged",
        string sourceContainer = "gdpr-exports",
        Guid? sourceBlobId = null,
        string entryPath = "identity-local.json",
        DateTimeOffset? expiresAt = null) =>
        new(
            RequestId: requestId ?? Guid.NewGuid(),
            SubjectUserId: subjectUserId ?? Guid.NewGuid(),
            ProviderName: providerName,
            FragmentKind: fragmentKind,
            SourceContainer: sourceContainer,
            SourceBlobId: sourceBlobId ?? Guid.NewGuid(),
            EntryPath: entryPath,
            ExpiresAt: expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30));

    // ────────────────────────────────────────────────────────────────────────
    // Happy path — sign + verify round-trip
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sign_then_verify_returns_true()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string tag = signer.Sign(in p);

        signer.Verify(in p, tag).ShouldBeTrue();
    }

    [Fact]
    public void Tag_starts_with_v1_prefix()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string tag = signer.Sign(in p);

        tag.ShouldStartWith("v1:");
    }

    [Fact]
    public void Tag_is_deterministic_for_same_parameters_and_key()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string first = signer.Sign(in p);
        string second = signer.Sign(in p);

        first.ShouldBe(second);
    }

    // ────────────────────────────────────────────────────────────────────────
    // VULN-001 / VULN-102 — any parameter change invalidates the tag
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Verify_rejects_when_request_id_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters();
        string tag = signer.Sign(in signed);

        ExportHmacParameters tampered = signed with { RequestId = Guid.NewGuid() };

        signer.Verify(in tampered, tag).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_when_subject_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters();
        string tag = signer.Sign(in signed);

        ExportHmacParameters tampered = signed with { SubjectUserId = Guid.NewGuid() };

        signer.Verify(in tampered, tag).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_when_source_blob_id_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters();
        string tag = signer.Sign(in signed);

        ExportHmacParameters tampered = signed with { SourceBlobId = Guid.NewGuid() };

        signer.Verify(in tampered, tag).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_when_entry_path_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(entryPath: "identity-local.json");
        string tag = signer.Sign(in signed);

        ExportHmacParameters tampered = signed with { EntryPath = "auditing.json" };

        signer.Verify(in tampered, tag).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_when_fragment_kind_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(fragmentKind: "staged");
        string tag = signer.Sign(in signed);

        ExportHmacParameters tampered = signed with { FragmentKind = "passthrough" };

        signer.Verify(in tampered, tag).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_when_source_container_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(sourceContainer: "gdpr-exports");
        string tag = signer.Sign(in signed);

        ExportHmacParameters tampered = signed with { SourceContainer = "other-container" };

        signer.Verify(in tampered, tag).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_when_provider_name_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(providerName: "identity-local");
        string tag = signer.Sign(in signed);

        ExportHmacParameters tampered = signed with { ProviderName = "auditing" };

        signer.Verify(in tampered, tag).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tampered tag bytes
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Verify_rejects_single_byte_flip_in_tag()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();
        string tag = signer.Sign(in p);

        // Flip the first char after "v1:" — base64url has a wide enough alphabet that
        // shifting by 1 stays valid syntactically but the HMAC bytes no longer match.
        const string prefix = "v1:";
        char first = tag[prefix.Length];
        char swapped = first == 'A' ? 'B' : 'A';
        string tampered = prefix + swapped + tag[(prefix.Length + 1)..];

        signer.Verify(in p, tampered).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("notatag")]
    [InlineData(":payload")]
    [InlineData("v1:")]
    [InlineData("v1:not_valid_base64!")]
    public void Verify_rejects_malformed_tag(string malformed)
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        if (string.IsNullOrEmpty(malformed))
        {
            Should.Throw<ArgumentException>(() => signer.Verify(in p, malformed));
        }
        else
        {
            signer.Verify(in p, malformed).ShouldBeFalse();
        }
    }

    [Fact]
    public void Verify_rejects_unknown_version_prefix()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();
        string tag = signer.Sign(in p);

        // Strip the v1 prefix, replace with v2 — same payload bytes but wrong version.
        string payload = tag["v1:".Length..];
        string forged = "v2:" + payload;

        signer.Verify(in p, forged).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Replay protection — expired tags
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Verify_rejects_expired_tag()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters expired = SampleParameters(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        string tag = signer.Sign(in expired);

        signer.Verify(in expired, tag).ShouldBeFalse();
    }

    [Fact]
    public void Verify_accepts_tag_about_to_expire()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters nearExpiry = SampleParameters(expiresAt: DateTimeOffset.UtcNow.AddSeconds(5));
        string tag = signer.Sign(in nearExpiry);

        signer.Verify(in nearExpiry, tag).ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Cross-instance — different ephemeral keys do not verify each other
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Tags_signed_by_one_signer_do_not_verify_under_another()
    {
        using EphemeralExportHmacSigner alice = CreateSigner();
        using EphemeralExportHmacSigner bob = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string aliceTag = alice.Sign(in p);

        bob.Verify(in p, aliceTag).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Disposal
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sign_after_dispose_throws()
    {
        EphemeralExportHmacSigner signer = CreateSigner();
        signer.Dispose();

        Should.Throw<ObjectDisposedException>(() => signer.Sign(SampleParameters()));
    }

    [Fact]
    public void Verify_after_dispose_throws()
    {
        EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();
        signer.Dispose();

        Should.Throw<ObjectDisposedException>(() => signer.Verify(in p, "v1:foo"));
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        EphemeralExportHmacSigner signer = CreateSigner();

        signer.Dispose();
        Should.NotThrow(() => signer.Dispose());
    }
}
