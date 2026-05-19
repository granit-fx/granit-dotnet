using System.Net;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Granit.Vault.Aws.Services;
using Granit.Vault.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.Aws.Tests;

public sealed class AwsSecretStoreTests
{
    private readonly IAmazonSecretsManager _secretsManager = Substitute.For<IAmazonSecretsManager>();
    private readonly AwsSecretStore _concrete;
    private readonly ISecretStore _sut;

    public AwsSecretStoreTests()
    {
        _concrete = new AwsSecretStore(_secretsManager, NullLogger<AwsSecretStore>.Instance);
        _sut = _concrete;
    }

    [Fact]
    public async Task GetSecretAsync_WithSecretString_ReturnsStringDescriptor()
    {
        _secretsManager.GetSecretValueAsync(
                Arg.Is<GetSecretValueRequest>(r => r.SecretId == "api-key" && r.VersionId == null),
                Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse
            {
                Name = "api-key",
                VersionId = "11111111-2222-3333-4444-555555555555",
                SecretString = "super-token",
                CreatedDate = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
            });

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("api-key"), TestContext.Current.CancellationToken);

        descriptor.StringValue.ShouldBe("super-token");
        descriptor.IsBinary.ShouldBeFalse();
        descriptor.Version.ShouldBe("11111111-2222-3333-4444-555555555555");
        descriptor.CreatedAt.ShouldNotBeNull();
        DateTimeOffset createdAt = descriptor.CreatedAt!.Value;
        createdAt.Year.ShouldBe(2026);
    }

    [Fact]
    public async Task GetSecretAsync_WithSecretBinary_ReturnsBinaryDescriptor()
    {
        byte[] payload = [0x10, 0x20, 0x30, 0x40];
        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse
            {
                Name = "mqtt-cert",
                VersionId = "99999999-8888-7777-6666-555555555555",
                SecretBinary = new MemoryStream(payload),
            });

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("mqtt-cert"), TestContext.Current.CancellationToken);

        descriptor.IsBinary.ShouldBeTrue();
        descriptor.BinaryValue!.Value.ToArray().ShouldBe(payload);
        descriptor.StringValue.ShouldBeNull();
        descriptor.Version.ShouldBe("99999999-8888-7777-6666-555555555555");
    }

    [Fact]
    public async Task GetSecretAsync_WithExplicitVersion_PassesVersionToSdk()
    {
        _secretsManager.GetSecretValueAsync(
                Arg.Is<GetSecretValueRequest>(r => r.SecretId == "api-key" && r.VersionId == "77777777-6666-5555-4444-333333333333"),
                Arg.Any<CancellationToken>())
            .Returns(new GetSecretValueResponse
            {
                Name = "api-key",
                VersionId = "77777777-6666-5555-4444-333333333333",
                SecretString = "v7-token",
            });

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.At("api-key", "77777777-6666-5555-4444-333333333333"), TestContext.Current.CancellationToken);

        descriptor.Version.ShouldBe("77777777-6666-5555-4444-333333333333");
        descriptor.StringValue.ShouldBe("v7-token");
    }

    [Fact]
    public async Task GetSecretAsync_WhenNotFound_ThrowsSecretNotFoundException()
    {
        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ResourceNotFoundException("not found"));

        SecretNotFoundException ex = await Should.ThrowAsync<SecretNotFoundException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("missing"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("missing");
    }

    [Fact]
    public async Task GetSecretAsync_WhenAccessDenied_ThrowsAccessDeniedException()
    {
        AmazonSecretsManagerException denied = new("denied")
        {
            ErrorCode = "AccessDeniedException",
        };

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(denied);

        SecretAccessDeniedException ex = await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("locked");
    }

    [Fact]
    public async Task GetSecretAsync_WhenThrottling_ThrowsTransientException()
    {
        AmazonSecretsManagerException throttled = new("throttled")
        {
            ErrorCode = "ThrottlingException",
        };

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(throttled);

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetSecretAsync_WhenHttpTransientStatus_ThrowsTransientException(HttpStatusCode status)
    {
        AmazonSecretsManagerException transient = new("transient")
        {
            StatusCode = status,
        };

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(transient);

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenNotFound_ReturnsNull()
    {
        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ResourceNotFoundException("not found"));

        SecretDescriptor? result = await _sut.TryGetSecretAsync(
            SecretRequest.Latest("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenAccessDenied_Bubbles()
    {
        AmazonSecretsManagerException denied = new("denied")
        {
            ErrorCode = "AccessDeniedException",
        };

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(denied);

        await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.TryGetSecretAsync(SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenTransient_Bubbles()
    {
        AmazonSecretsManagerException transient = new("transient")
        {
            StatusCode = HttpStatusCode.ServiceUnavailable,
        };

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(transient);

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.TryGetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_CancellationIsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _secretsManager.GetSecretValueAsync(Arg.Any<GetSecretValueRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("slow"), cts.Token));
    }
}
