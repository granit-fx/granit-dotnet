using Granit.Guids;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Options;
using Granit.Hostnames.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Tests.Services;

public sealed class HostnameRegistrationServiceTests
{
    private static readonly Guid FixedId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TokenId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OwnerId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly IManagedHostnameReader _reader = Substitute.For<IManagedHostnameReader>();
    private readonly IManagedHostnameWriter _writer = Substitute.For<IManagedHostnameWriter>();
    private readonly IGuidGenerator _guids = Substitute.For<IGuidGenerator>();

    private HostnameRegistrationService BuildSut(HostnamesOptions? opts = null) =>
        new(_reader, _writer, _guids,
            Microsoft.Extensions.Options.Options.Create(opts ?? new HostnamesOptions()));

    // ── RegisterAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_NewHost_WithIngress_Returns_Succeeded_With_Verifying_Hostname()
    {
        _guids.Create().Returns(FixedId, TokenId);
        _reader.FindByHostAsync("acme.com", Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        var opts = new HostnamesOptions
        {
            IngressTarget = "ingress.platform.example.com",
            TxtChallengePrefix = "_granit-challenge",
        };
        HostnameRegistrationService sut = BuildSut(opts);

        HostnameRegistrationResult result = await sut.RegisterAsync(
            "acme.com", "cms.site", OwnerId, cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(HostnameRegistrationOutcome.Succeeded);
        result.Hostname.ShouldNotBeNull();
        result.Hostname.Status.ShouldBe(HostnameStatus.Verifying);
        result.Hostname.VerificationToken.ShouldBe(TokenId.ToString("N"));
        result.Hostname.ExpectedDnsRecords.Count.ShouldBe(2);
        result.Hostname.ExpectedDnsRecords.ShouldContain(r =>
            r.RecordType == DnsRecordType.Cname && r.Value == "ingress.platform.example.com");
        result.Hostname.ExpectedDnsRecords.ShouldContain(r =>
            r.RecordType == DnsRecordType.Txt && r.Name == "_granit-challenge.acme.com");

        await _writer.Received(1).AddAsync(result.Hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_NewHost_WithoutIngress_Returns_Succeeded_With_Pending_Hostname()
    {
        _guids.Create().Returns(FixedId);
        _reader.FindByHostAsync("acme.com", Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        HostnameRegistrationService sut = BuildSut();

        HostnameRegistrationResult result = await sut.RegisterAsync(
            "acme.com", "cms.site", OwnerId, cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(HostnameRegistrationOutcome.Succeeded);
        result.Hostname.ShouldNotBeNull();
        result.Hostname.Status.ShouldBe(HostnameStatus.Pending);
        result.Hostname.ExpectedDnsRecords.ShouldBeEmpty();
        result.Hostname.VerificationToken.ShouldBeNull();

        await _writer.Received(1).AddAsync(result.Hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_DuplicateHost_Returns_HostAlreadyTaken_Without_Writing()
    {
        var existing = ManagedHostname.Create(FixedId, "acme.com", "other.owner", OwnerId);
        _reader.FindByHostAsync("acme.com", Arg.Any<CancellationToken>())
            .Returns(existing);

        HostnameRegistrationService sut = BuildSut();

        HostnameRegistrationResult result = await sut.RegisterAsync(
            "acme.com", "cms.site", OwnerId, cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(HostnameRegistrationOutcome.HostAlreadyTaken);
        result.Hostname.ShouldBeNull();

        await _writer.DidNotReceive().AddAsync(Arg.Any<ManagedHostname>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_InvalidFqdn_Throws_ArgumentException()
    {
        HostnameRegistrationService sut = BuildSut();

        await Should.ThrowAsync<ArgumentException>(
            async () => await sut.RegisterAsync(
                "not!!valid", "cms.site", OwnerId,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Register_Stores_IsPrimary_Flag()
    {
        _guids.Create().Returns(FixedId);
        _reader.FindByHostAsync("acme.com", Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        HostnameRegistrationService sut = BuildSut();
        HostnameRegistrationResult result = await sut.RegisterAsync(
            "acme.com", "cms.site", OwnerId, isPrimary: true,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(HostnameRegistrationOutcome.Succeeded);
        result.Hostname!.IsPrimary.ShouldBeTrue();
    }

    // ── RequestVerificationAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RequestVerification_PendingHostname_WithIngress_Returns_Succeeded_And_Transitions_To_Verifying()
    {
        var hostname = ManagedHostname.Create(FixedId, "acme.com", "cms.site", OwnerId);
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);
        _guids.Create().Returns(TokenId);

        var opts = new HostnamesOptions { IngressTarget = "ingress.platform.example.com" };
        HostnameRegistrationService sut = BuildSut(opts);

        RequestVerificationResult result = await sut.RequestVerificationAsync(
            FixedId, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(RequestVerificationOutcome.Succeeded);
        result.Hostname.ShouldNotBeNull();
        result.Hostname.Status.ShouldBe(HostnameStatus.Verifying);
        result.Hostname.VerificationToken.ShouldBe(TokenId.ToString("N"));

        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestVerification_PendingHostname_WithoutIngress_Returns_IngressNotConfigured()
    {
        var hostname = ManagedHostname.Create(FixedId, "acme.com", "cms.site", OwnerId);
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        HostnameRegistrationService sut = BuildSut();

        RequestVerificationResult result = await sut.RequestVerificationAsync(
            FixedId, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(RequestVerificationOutcome.IngressNotConfigured);
        result.Hostname.ShouldBeNull();
        hostname.Status.ShouldBe(HostnameStatus.Pending);

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ManagedHostname>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestVerification_ActiveHostname_Returns_Succeeded_Via_RequestRecheck()
    {
        var hostname = ManagedHostname.Create(FixedId, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("tok", [new ExpectedDnsRecord(DnsRecordType.Cname, "acme.com", "ingress.platform.example.com")]);
        hostname.MarkVerified(DateTimeOffset.UtcNow);
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        HostnameRegistrationService sut = BuildSut();

        RequestVerificationResult result = await sut.RequestVerificationAsync(
            FixedId, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(RequestVerificationOutcome.Succeeded);
        result.Hostname!.Status.ShouldBe(HostnameStatus.Verifying);

        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestVerification_ErrorHostname_Returns_Succeeded_Via_RequestRecheck()
    {
        var hostname = ManagedHostname.Create(FixedId, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("tok", [new ExpectedDnsRecord(DnsRecordType.Cname, "acme.com", "ingress.platform.example.com")]);
        hostname.MarkFailed([], DateTimeOffset.UtcNow);
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>()).Returns(hostname);

        HostnameRegistrationService sut = BuildSut();

        RequestVerificationResult result = await sut.RequestVerificationAsync(
            FixedId, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(RequestVerificationOutcome.Succeeded);
        result.Hostname!.Status.ShouldBe(HostnameStatus.Verifying);

        await _writer.Received(1).UpdateAsync(hostname, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestVerification_UnknownId_Returns_NotFound()
    {
        _reader.GetByIdAsync(FixedId, Arg.Any<CancellationToken>())
            .Returns((ManagedHostname?)null);

        HostnameRegistrationService sut = BuildSut();

        RequestVerificationResult result = await sut.RequestVerificationAsync(
            FixedId, TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(RequestVerificationOutcome.NotFound);
        result.Hostname.ShouldBeNull();

        await _writer.DidNotReceive().UpdateAsync(Arg.Any<ManagedHostname>(), Arg.Any<CancellationToken>());
    }
}
