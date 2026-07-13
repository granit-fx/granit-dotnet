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
    public async Task Sign_then_verify_returns_true()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string tag = await signer.SignAsync(p, TestContext.Current.CancellationToken);

        (await signer.VerifyAsync(p, tag, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task Tag_starts_with_v1_prefix()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string tag = await signer.SignAsync(p, TestContext.Current.CancellationToken);

        tag.ShouldStartWith("v1:");
    }

    [Fact]
    public async Task Tag_is_deterministic_for_same_parameters_and_key()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string first = await signer.SignAsync(p, TestContext.Current.CancellationToken);
        string second = await signer.SignAsync(p, TestContext.Current.CancellationToken);

        first.ShouldBe(second);
    }

    // ────────────────────────────────────────────────────────────────────────
    // VULN-001 / VULN-102 — any parameter change invalidates the tag
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_rejects_when_request_id_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters();
        string tag = await signer.SignAsync(signed, TestContext.Current.CancellationToken);

        ExportHmacParameters tampered = signed with { RequestId = Guid.NewGuid() };

        (await signer.VerifyAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_rejects_when_subject_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters();
        string tag = await signer.SignAsync(signed, TestContext.Current.CancellationToken);

        ExportHmacParameters tampered = signed with { SubjectUserId = Guid.NewGuid() };

        (await signer.VerifyAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_rejects_when_source_blob_id_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters();
        string tag = await signer.SignAsync(signed, TestContext.Current.CancellationToken);

        ExportHmacParameters tampered = signed with { SourceBlobId = Guid.NewGuid() };

        (await signer.VerifyAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_rejects_when_entry_path_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(entryPath: "identity-local.json");
        string tag = await signer.SignAsync(signed, TestContext.Current.CancellationToken);

        ExportHmacParameters tampered = signed with { EntryPath = "auditing.json" };

        (await signer.VerifyAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_rejects_when_fragment_kind_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(fragmentKind: "staged");
        string tag = await signer.SignAsync(signed, TestContext.Current.CancellationToken);

        ExportHmacParameters tampered = signed with { FragmentKind = "passthrough" };

        (await signer.VerifyAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_rejects_when_source_container_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(sourceContainer: "gdpr-exports");
        string tag = await signer.SignAsync(signed, TestContext.Current.CancellationToken);

        ExportHmacParameters tampered = signed with { SourceContainer = "other-container" };

        (await signer.VerifyAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_rejects_when_provider_name_differs()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters signed = SampleParameters(providerName: "identity-local");
        string tag = await signer.SignAsync(signed, TestContext.Current.CancellationToken);

        ExportHmacParameters tampered = signed with { ProviderName = "auditing" };

        (await signer.VerifyAsync(tampered, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Tampered tag bytes
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_rejects_single_byte_flip_in_tag()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();
        string tag = await signer.SignAsync(p, TestContext.Current.CancellationToken);

        // Flip the first char after "v1:" — base64url has a wide enough alphabet that
        // shifting by 1 stays valid syntactically but the HMAC bytes no longer match.
        const string prefix = "v1:";
        char first = tag[prefix.Length];
        char swapped = first == 'A' ? 'B' : 'A';
        string tampered = prefix + swapped + tag[(prefix.Length + 1)..];

        (await signer.VerifyAsync(p, tampered, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("notatag")]
    [InlineData(":payload")]
    [InlineData("v1:")]
    [InlineData("v1:not_valid_base64!")]
    public async Task Verify_rejects_malformed_tag(string malformed)
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        if (string.IsNullOrEmpty(malformed))
        {
            await Should.ThrowAsync<ArgumentException>(() => signer.VerifyAsync(p, malformed, TestContext.Current.CancellationToken));
        }
        else
        {
            (await signer.VerifyAsync(p, malformed, TestContext.Current.CancellationToken)).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Verify_rejects_unknown_version_prefix()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();
        string tag = await signer.SignAsync(p, TestContext.Current.CancellationToken);

        // Strip the v1 prefix, replace with v2 — same payload bytes but wrong version.
        string payload = tag["v1:".Length..];
        string forged = "v2:" + payload;

        (await signer.VerifyAsync(p, forged, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Replay protection — expired tags
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_rejects_expired_tag()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters expired = SampleParameters(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        string tag = await signer.SignAsync(expired, TestContext.Current.CancellationToken);

        (await signer.VerifyAsync(expired, tag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Verify_accepts_tag_about_to_expire()
    {
        using EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters nearExpiry = SampleParameters(expiresAt: DateTimeOffset.UtcNow.AddSeconds(5));
        string tag = await signer.SignAsync(nearExpiry, TestContext.Current.CancellationToken);

        (await signer.VerifyAsync(nearExpiry, tag, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Cross-instance — different ephemeral keys do not verify each other
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Tags_signed_by_one_signer_do_not_verify_under_another()
    {
        using EphemeralExportHmacSigner alice = CreateSigner();
        using EphemeralExportHmacSigner bob = CreateSigner();
        ExportHmacParameters p = SampleParameters();

        string aliceTag = await alice.SignAsync(p, TestContext.Current.CancellationToken);

        (await bob.VerifyAsync(p, aliceTag, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    // ────────────────────────────────────────────────────────────────────────
    // Disposal
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sign_after_dispose_throws()
    {
        EphemeralExportHmacSigner signer = CreateSigner();
        signer.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(() => signer.SignAsync(SampleParameters(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Verify_after_dispose_throws()
    {
        EphemeralExportHmacSigner signer = CreateSigner();
        ExportHmacParameters p = SampleParameters();
        signer.Dispose();

        await Should.ThrowAsync<ObjectDisposedException>(() => signer.VerifyAsync(p, "v1:foo", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        EphemeralExportHmacSigner signer = CreateSigner();

        signer.Dispose();
        Should.NotThrow(() => signer.Dispose());
    }
}
