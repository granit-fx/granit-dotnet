using System.Text;
using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Vault.Options;
using Granit.Vault;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Vault.Tests;

public sealed class VaultExportHmacSignerTests
{
    private readonly ITransitMacService _macService = Substitute.For<ITransitMacService>();
    private readonly VaultExportHmacSigner _sut;

    public VaultExportHmacSignerTests()
    {
        VaultExportSignerOptions options = new()
        {
            FragmentKeyName = "fragment-key",
            ContentKeyName = "content-key",
        };
        _sut = new VaultExportHmacSigner(
            _macService,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<VaultExportHmacSigner>.Instance);
    }

    private static ExportHmacParameters MakeParameters(DateTimeOffset? expiry = null) => new(
        RequestId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        SubjectUserId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
        ProviderName: "test",
        FragmentKind: "staged",
        SourceContainer: "exports",
        SourceBlobId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
        EntryPath: "/test/file.json",
        ExpiresAt: expiry ?? DateTimeOffset.UtcNow.AddMinutes(30));

    [Fact]
    public void Sign_WrapsProviderTag_WithFrameworkPrefix()
    {
        _macService.MacAsync(
                "fragment-key",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TransitMacResult("vault:v1:opaque", 1));

        string tag = _sut.Sign(MakeParameters());

        tag.ShouldBe("gpv1:vault:v1:opaque");
    }

    [Fact]
    public void Verify_StripsPrefix_AndDelegatesToMacService()
    {
        _macService.VerifyAsync(
                "fragment-key",
                Arg.Any<ReadOnlyMemory<byte>>(),
                "vault:v1:opaque",
                Arg.Any<CancellationToken>())
            .Returns(true);

        _sut.Verify(MakeParameters(), "gpv1:vault:v1:opaque").ShouldBeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForLegacyEphemeralTagFormat()
    {
        // Tag from EphemeralExportHmacSigner — vN:base64 with no gpv1 prefix.
        _sut.Verify(MakeParameters(), "v1:abc123").ShouldBeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenTagIsExpired()
    {
        _sut.Verify(
                MakeParameters(expiry: DateTimeOffset.UtcNow.AddMinutes(-1)),
                "gpv1:vault:v1:opaque")
            .ShouldBeFalse();
    }

    [Fact]
    public void SignBytes_UsesContentKeyName()
    {
        _macService.MacAsync(
                "content-key",
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TransitMacResult("provider:tag", 1));

        string tag = _sut.SignBytes(Encoding.UTF8.GetBytes("manifest-bytes"));

        tag.ShouldBe("gpv1:provider:tag");
    }

    [Fact]
    public void VerifyBytes_RejectsLegacyTagFormat()
    {
        _sut.VerifyBytes(Encoding.UTF8.GetBytes("manifest-bytes"), "v1:abc").ShouldBeFalse();
    }
}
