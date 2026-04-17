using global::Azure;
using global::Azure.Security.KeyVault.Secrets;
using Granit.Vault;
using Granit.Vault.Azure.Services;
using Granit.Vault.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.Azure.Tests;

public sealed class AzureSecretStoreTests
{
    private readonly SecretClient _secretClient = Substitute.For<SecretClient>();
    private readonly AzureSecretStore _concrete;
    private readonly ISecretStore _sut;

    public AzureSecretStoreTests()
    {
        _concrete = new AzureSecretStore(_secretClient, NullLogger<AzureSecretStore>.Instance);
        _sut = _concrete;
    }

    [Fact]
    public async Task GetSecretAsync_WithStringSecret_ReturnsStringDescriptor()
    {
        var properties = new SecretProperties(new Uri("https://kv.vault.azure.net/secrets/api-key/v1"));
        properties.ContentType = "text/plain";
        KeyVaultSecret secret = SecretModelFactory.KeyVaultSecret(
            properties: properties,
            value: "super-token");

        _secretClient.GetSecretAsync("api-key", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(secret, Substitute.For<Response>())));

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("api-key"), TestContext.Current.CancellationToken);

        descriptor.StringValue.ShouldBe("super-token");
        descriptor.IsBinary.ShouldBeFalse();
        descriptor.ContentType.ShouldBe("text/plain");
        descriptor.Version.ShouldBe("v1");
    }

    [Fact]
    public async Task GetSecretAsync_WithBinaryContentType_DecodesBase64()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0x04];
        var properties = new SecretProperties(new Uri("https://kv.vault.azure.net/secrets/mqtt-cert/abc"));
        properties.ContentType = "application/octet-stream";
        KeyVaultSecret secret = SecretModelFactory.KeyVaultSecret(
            properties: properties,
            value: Convert.ToBase64String(payload));

        _secretClient.GetSecretAsync("mqtt-cert", null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(secret, Substitute.For<Response>())));

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("mqtt-cert"), TestContext.Current.CancellationToken);

        descriptor.IsBinary.ShouldBeTrue();
        descriptor.BinaryValue!.Value.ToArray().ShouldBe(payload);
        descriptor.ContentType.ShouldBe("application/octet-stream");
        descriptor.Version.ShouldBe("abc");
    }

    [Fact]
    public async Task GetSecretAsync_WithExplicitVersion_PassesVersionToSdk()
    {
        var properties = new SecretProperties(new Uri("https://kv.vault.azure.net/secrets/api-key/v7"));
        KeyVaultSecret secret = SecretModelFactory.KeyVaultSecret(
            properties: properties,
            value: "v7-token");

        _secretClient.GetSecretAsync("api-key", "v7", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(secret, Substitute.For<Response>())));

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.At("api-key", "v7"), TestContext.Current.CancellationToken);

        descriptor.StringValue.ShouldBe("v7-token");
        descriptor.Version.ShouldBe("v7");
        await _secretClient.Received(1).GetSecretAsync("api-key", "v7", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetSecretAsync_WhenNotFound_ThrowsSecretNotFoundException()
    {
        _secretClient.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(status: 404, message: "not found"));

        SecretNotFoundException ex = await Should.ThrowAsync<SecretNotFoundException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("missing"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("missing");
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task GetSecretAsync_WhenUnauthorizedOrForbidden_ThrowsAccessDenied(int status)
    {
        _secretClient.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(status: status, message: "denied"));

        SecretAccessDeniedException ex = await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("locked");
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task GetSecretAsync_WhenTransient_ThrowsTransientException(int status)
    {
        _secretClient.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(status: status, message: "transient"));

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenNotFound_ReturnsNull()
    {
        _secretClient.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(status: 404, message: "not found"));

        SecretDescriptor? result = await _sut.TryGetSecretAsync(
            SecretRequest.Latest("missing"), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenAccessDenied_Bubbles()
    {
        _secretClient.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(status: 403, message: "denied"));

        await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.TryGetSecretAsync(SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenTransient_Bubbles()
    {
        _secretClient.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(status: 503, message: "transient"));

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.TryGetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_CancellationIsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _secretClient.GetSecretAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("slow"), cts.Token));
    }
}
