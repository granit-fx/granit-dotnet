using System.Net;
using Granit.Vault;
using Granit.Vault.Exceptions;
using Granit.Vault.HashiCorp.Options;
using Granit.Vault.HashiCorp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.KeyValue;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using Xunit;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class HashiCorpSecretStoreTests
{
    private readonly IVaultClient _vaultClient;
    private readonly IKeyValueSecretsEngineV2 _kvV2;
    private readonly HashiCorpSecretStore _concrete;
    private readonly ISecretStore _sut;

    public HashiCorpSecretStoreTests()
    {
        _vaultClient = Substitute.For<IVaultClient>();
        _kvV2 = Substitute.For<IKeyValueSecretsEngineV2>();

        IVaultClientV1 v1 = Substitute.For<IVaultClientV1>();
        ISecretsEngine secrets = Substitute.For<ISecretsEngine>();
        IKeyValueSecretsEngine kv = Substitute.For<IKeyValueSecretsEngine>();

        _vaultClient.V1.Returns(v1);
        v1.Secrets.Returns(secrets);
        secrets.KeyValue.Returns(kv);
        kv.V2.Returns(_kvV2);

        IOptions<HashiCorpVaultOptions> options = Microsoft.Extensions.Options.Options.Create(new HashiCorpVaultOptions
        {
            KvMountPoint = "secret",
        });

        _concrete = new HashiCorpSecretStore(
            _vaultClient,
            options,
            NullLogger<HashiCorpSecretStore>.Instance);
        _sut = _concrete;
    }

    [Fact]
    public async Task GetSecretAsync_WithStringSecret_ReturnsStringDescriptor()
    {
        _kvV2.ReadSecretAsync("api/key", null, "secret")
            .Returns(Task.FromResult(CreateSecret(
                data: new Dictionary<string, object> { ["value"] = "super-token" },
                version: 3,
                createdTime: "2026-04-01T10:00:00Z")));

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("api/key"),
            TestContext.Current.CancellationToken);

        descriptor.Name.ShouldBe("api/key");
        descriptor.StringValue.ShouldBe("super-token");
        descriptor.BinaryValue.ShouldBeNull();
        descriptor.IsBinary.ShouldBeFalse();
        descriptor.Version.ShouldBe("3");
        descriptor.CreatedAt.ShouldNotBeNull();
        DateTimeOffset createdAt = descriptor.CreatedAt!.Value;
        createdAt.Year.ShouldBe(2026);
    }

    [Fact]
    public async Task GetSecretAsync_WithBinarySecret_ReturnsBinaryDescriptor()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0x04];
        _kvV2.ReadSecretAsync("mqtt/cert", null, "secret")
            .Returns(Task.FromResult(CreateSecret(
                data: new Dictionary<string, object> { ["__binary"] = Convert.ToBase64String(payload) },
                version: 1)));

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.Latest("mqtt/cert"),
            TestContext.Current.CancellationToken);

        descriptor.IsBinary.ShouldBeTrue();
        descriptor.BinaryValue.ShouldNotBeNull();
        descriptor.BinaryValue!.Value.ToArray().ShouldBe(payload);
        descriptor.StringValue.ShouldBeNull();
        descriptor.ContentType.ShouldBe("application/octet-stream");
    }

    [Fact]
    public async Task GetSecretAsync_WithExplicitVersion_PassesVersionToSdk()
    {
        _kvV2.ReadSecretAsync("api/key", 7, "secret")
            .Returns(Task.FromResult(CreateSecret(
                data: new Dictionary<string, object> { ["value"] = "v7-token" },
                version: 7)));

        SecretDescriptor descriptor = await _sut.GetSecretAsync(
            SecretRequest.At("api/key", "7"),
            TestContext.Current.CancellationToken);

        descriptor.StringValue.ShouldBe("v7-token");
        descriptor.Version.ShouldBe("7");
        await _kvV2.Received(1).ReadSecretAsync("api/key", 7, "secret");
    }

    [Fact]
    public async Task GetSecretAsync_WithInvalidVersion_ThrowsConfigurationException()
    {
        await Should.ThrowAsync<SecretVaultConfigurationException>(async () =>
            await _sut.GetSecretAsync(
                SecretRequest.At("api/key", "not-a-number"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_WhenNotFound_ThrowsSecretNotFoundException()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.NotFound, "not found"));

        SecretNotFoundException ex = await Should.ThrowAsync<SecretNotFoundException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("missing"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("missing");
    }

    [Fact]
    public async Task GetSecretAsync_WhenForbidden_ThrowsSecretAccessDeniedException()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.Forbidden, "denied"));

        SecretAccessDeniedException ex = await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("locked");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GetSecretAsync_WhenTransientStatus_ThrowsTransientException(HttpStatusCode status)
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(status, "transient"));

        SecretVaultTransientException ex = await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));

        ex.SecretName.ShouldBe("flaky");
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenNotFound_ReturnsNull()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.NotFound, "not found"));

        SecretDescriptor? result = await _sut.TryGetSecretAsync(
            SecretRequest.Latest("missing"),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenAccessDenied_Bubbles()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.Forbidden, "denied"));

        await Should.ThrowAsync<SecretAccessDeniedException>(async () =>
            await _sut.TryGetSecretAsync(
                SecretRequest.Latest("locked"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryGetSecretAsync_WhenTransient_Bubbles()
    {
        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .ThrowsAsync(new VaultApiException(HttpStatusCode.ServiceUnavailable, "oops"));

        await Should.ThrowAsync<SecretVaultTransientException>(async () =>
            await _sut.TryGetSecretAsync(
                SecretRequest.Latest("flaky"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_CancellationIsPropagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _kvV2.ReadSecretAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>())
            .Returns(async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cts.Token);
                return CreateSecret([], version: 1);
            });

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _sut.GetSecretAsync(SecretRequest.Latest("slow"), cts.Token));
    }

    private static Secret<SecretData> CreateSecret(
        Dictionary<string, object> data,
        int version,
        string? createdTime = null) =>
        new()
        {
            Data = new SecretData
            {
                Data = data,
                Metadata = new CurrentSecretMetadata
                {
                    Version = version,
                    CreatedTime = createdTime ?? string.Empty,
                },
            },
        };
}
