using System.Diagnostics.Metrics;
using System.Text;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Granit.Vault.Aws.Options;
using Granit.Vault.Aws.Services;
using Granit.Vault.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class AwsKmsMacServiceTests : IDisposable
{
    private readonly IAmazonKeyManagementService _kms = Substitute.For<IAmazonKeyManagementService>();
    private readonly ServiceProvider _sp;
    private readonly VaultMetrics _metrics;

    public AwsKmsMacServiceTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        _metrics = new VaultMetrics(_sp.GetRequiredService<IMeterFactory>());
    }

    public void Dispose() => _sp.Dispose();

    private AwsKmsMacService BuildSut(string? previousAlias = null)
    {
        AwsKmsMacOptions options = new()
        {
            CurrentAlias = "alias/granit-test-current",
            PreviousAlias = previousAlias,
        };
        return new AwsKmsMacService(
            _kms,
            Microsoft.Extensions.Options.Options.Create(options),
            _metrics,
            currentTenant: null,
            NullLogger<AwsKmsMacService>.Instance);
    }

    private static GenerateMacResponse MakeMac(byte[] mac) => new()
    {
        Mac = new MemoryStream(mac, writable: false),
        KeyId = "arn:aws:kms:eu-west-1:000:key/abc",
        MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256,
    };

    private static VerifyMacResponse MakeVerify(bool valid) => new()
    {
        KeyId = "arn:aws:kms:eu-west-1:000:key/abc",
        MacAlgorithm = MacAlgorithmSpec.HMAC_SHA_256,
        MacValid = valid,
    };

    [Fact]
    public async Task MacAsync_SignsWithCurrentAlias_AndPrefixesTag()
    {
        byte[] expectedMac = [1, 2, 3, 4];
        _kms.GenerateMacAsync(
                Arg.Is<GenerateMacRequest>(r => r.KeyId == "alias/granit-test-current"),
                Arg.Any<CancellationToken>())
            .Returns(MakeMac(expectedMac));
        AwsKmsMacService sut = BuildSut();

        TransitMacResult result = await sut.MacAsync(
            "k",
            Encoding.UTF8.GetBytes("hello"),
            TestContext.Current.CancellationToken);

        result.Mac.ShouldStartWith("akms:current:");
        Convert.FromBase64String(result.Mac["akms:current:".Length..]).ShouldBe(expectedMac);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsTrue_WhenCurrentValid()
    {
        _kms.GenerateMacAsync(Arg.Any<GenerateMacRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeMac([1, 2, 3]));
        _kms.VerifyMacAsync(
                Arg.Is<VerifyMacRequest>(r => r.KeyId == "alias/granit-test-current"),
                Arg.Any<CancellationToken>())
            .Returns(MakeVerify(true));
        AwsKmsMacService sut = BuildSut();

        TransitMacResult signed = await sut.MacAsync("k", Encoding.UTF8.GetBytes("x"), TestContext.Current.CancellationToken);
        bool ok = await sut.VerifyAsync("k", Encoding.UTF8.GetBytes("x"), signed.Mac, TestContext.Current.CancellationToken);

        ok.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyAsync_FallbackPath_IssuesBothCalls()
    {
        // Tag advertises 'previous' — we expect both current and previous KMS calls
        // to be issued (constant-time defence) before the result is computed.
        _kms.VerifyMacAsync(Arg.Any<VerifyMacRequest>(), Arg.Any<CancellationToken>())
            .Returns(MakeVerify(false), MakeVerify(true));
        AwsKmsMacService sut = BuildSut(previousAlias: "alias/granit-test-previous");

        string tag = "akms:previous:" + Convert.ToBase64String([1, 2]);
        bool ok = await sut.VerifyAsync("k", Encoding.UTF8.GetBytes("x"), tag, TestContext.Current.CancellationToken);

        ok.ShouldBeTrue();
        await _kms.Received(2).VerifyMacAsync(Arg.Any<VerifyMacRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VerifyAsync_RejectsNonAkmsTag()
    {
        AwsKmsMacService sut = BuildSut();

        bool ok = await sut.VerifyAsync(
            "k",
            Encoding.UTF8.GetBytes("x"),
            "vault:v1:abc",
            TestContext.Current.CancellationToken);

        ok.ShouldBeFalse();
        await _kms.DidNotReceive().VerifyMacAsync(Arg.Any<VerifyMacRequest>(), Arg.Any<CancellationToken>());
    }
}
