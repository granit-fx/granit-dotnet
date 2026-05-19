using Google.Cloud.SecretManager.V1;
using Google.Protobuf;
using Granit.Vault.Exceptions;
using Granit.Vault.GoogleCloud.Options;
using Granit.Vault.GoogleCloud.Services;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.GoogleCloud.Tests;

public sealed class GoogleCloudSecretStoreTests
{
    private const string TestProject = "granit-test-proj";

    private readonly SecretManagerServiceClient _client = Substitute.For<SecretManagerServiceClient>();
    private readonly GoogleCloudSecretStore _concrete;
    private readonly ISecretStore _sut;

    public GoogleCloudSecretStoreTests()
    {
        IOptions<GoogleCloudVaultOptions> options = Microsoft.Extensions.Options.Options.Create(new GoogleCloudVaultOptions
        {
            ProjectId = TestProject,
            Location = "europe-west1",
            KeyRing = "kr",
            CryptoKey = "ck",
        });

        _concrete = new GoogleCloudSecretStore(
            _client, options, NullLogger<GoogleCloudSecretStore>.Instance);
        _sut = _concrete;
    }

    [Fact]
    public async Task GetSecretAsync_WithShortName_BuildsLatestVersionName()
    {
        // Invalid UTF-8 prefix (0xFF 0xFE is not a valid UTF-8 start sequence) forces
        // the binary facet — proves the short-name/latest path works independently of
        // the UTF-8 heuristic.
        byte[] payload = [0xFF, 0xFE, 0xFD];
        _client.AccessSecretVersionAsync(
                Arg.Is<SecretVersionName>(v => v.ProjectId == TestProject
                    && v.SecretId == "api-key"
                    && v.SecretVersionId == "latest"),
                Arg.Any<CancellationToken>())
            .Returns(new AccessSecretVersionResponse
            {
                Name = $"projects/{TestProject}/secrets/api-key/versions/42",
                Payload = new SecretPayload { Data = ByteString.CopyFrom(payload) },
            });

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("api-key"), TestContext.Current.CancellationToken);

        descriptor.IsBinary.ShouldBeTrue();
        descriptor.BinaryValue!.Value.ToArray().ShouldBe(payload);
        descriptor.Version.ShouldBe("42");
    }

    [Fact]
    public async Task GetSecretAsync_WithUtf8Payload_ReturnsStringFacet()
    {
        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .Returns(new AccessSecretVersionResponse
            {
                Name = $"projects/{TestProject}/secrets/api-key/versions/1",
                Payload = new SecretPayload { Data = ByteString.CopyFromUtf8("super-token") },
            });

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("api-key"), TestContext.Current.CancellationToken);

        descriptor.StringValue.ShouldBe("super-token");
        descriptor.IsBinary.ShouldBeFalse();
    }

    [Fact]
    public async Task GetSecretAsync_WithExplicitVersion_UsesThatVersion()
    {
        _client.AccessSecretVersionAsync(
                Arg.Is<SecretVersionName>(v => v.SecretVersionId == "7"),
                Arg.Any<CancellationToken>())
            .Returns(new AccessSecretVersionResponse
            {
                Name = $"projects/{TestProject}/secrets/api-key/versions/7",
                Payload = new SecretPayload { Data = ByteString.CopyFromUtf8("v7-token") },
            });

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.At("api-key", "7"), TestContext.Current.CancellationToken);

        descriptor.Version.ShouldBe("7");
    }

    [Fact]
    public async Task GetSecretAsync_WithQualifiedName_ParsesResource()
    {
        string resource = $"projects/{TestProject}/secrets/api-key/versions/latest";
        _client.AccessSecretVersionAsync(
                Arg.Is<SecretVersionName>(v => v.ProjectId == TestProject && v.SecretId == "api-key"),
                Arg.Any<CancellationToken>())
            .Returns(new AccessSecretVersionResponse
            {
                Name = $"projects/{TestProject}/secrets/api-key/versions/1",
                Payload = new SecretPayload { Data = ByteString.CopyFromUtf8("ok") },
            });

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest(resource), TestContext.Current.CancellationToken);

        // "ok" is valid UTF-8 → store prefers StringValue facet (heuristic).
        descriptor.StringValue.ShouldBe("ok");
    }

    [Fact]
    public async Task GetSecretAsync_WhenNotFound_ThrowsSecretNotFoundException()
    {
        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(StatusCode.NotFound, "not found")));

        SecretNotFoundException ex = await Should.ThrowAsync<SecretNotFoundException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("missing"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("missing");
    }

    [Theory]
    [InlineData(StatusCode.PermissionDenied)]
    [InlineData(StatusCode.Unauthenticated)]
    public async Task GetSecretAsync_WhenDenied_ThrowsAccessDeniedException(StatusCode code)
    {
        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(code, "denied")));

        SecretAccessDeniedException ex = await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("locked");
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    [InlineData(StatusCode.ResourceExhausted)]
    [InlineData(StatusCode.Internal)]
    public async Task GetSecretAsync_WhenTransient_ThrowsTransientException(StatusCode code)
    {
        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(code, "transient")));

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_WithShortName_WhenProjectIdMissing_ThrowsConfiguration()
    {
        GoogleCloudSecretStore store = new(
            _client,
            Microsoft.Extensions.Options.Options.Create(new GoogleCloudVaultOptions
            {
                ProjectId = string.Empty,
                Location = "global",
                KeyRing = "kr",
                CryptoKey = "ck",
            }),
            NullLogger<GoogleCloudSecretStore>.Instance);

        await Should.ThrowAsync<SecretVaultConfigurationException>(async () =>
            await store.GetSecretAsync(SecretRequest.Latest("api-key"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenNotFound_ReturnsNull()
    {
        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(StatusCode.NotFound, "not found")));

        SecretDescriptor? result = await _sut.TryGetSecretAsync(
            SecretRequest.Latest("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenDenied_Bubbles()
    {
        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(StatusCode.PermissionDenied, "denied")));

        await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.TryGetSecretAsync(SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenTransient_Bubbles()
    {
        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "transient")));

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.TryGetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_CancellationIsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _client.AccessSecretVersionAsync(Arg.Any<SecretVersionName>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("slow"), cts.Token));
    }
}
