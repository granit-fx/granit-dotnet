using DnsClient;
using DnsClient.Protocol;
using Granit.Hostnames.Contracts;
using Granit.Hostnames.Domain;
using Granit.Hostnames.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Tests.Services;

public sealed class DnsHostnameVerifierTests
{
    private static readonly Guid Id = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static ManagedHostname PendingHostname(string host = "acme.com")
    {
        var h = ManagedHostname.Create(Id, host, "cms.site", OwnerId);
        h.BeginVerification("token123",
        [
            new ExpectedDnsRecord(DnsRecordType.Cname, host, "ingress.platform.example.com"),
            new ExpectedDnsRecord(DnsRecordType.Txt, $"_granit-challenge.{host}", "granit-verify=token123"),
        ]);
        return h;
    }

    // ── No expected records ──────────────────────────────────────────────────

    [Fact]
    public async Task NoExpectedRecords_ReturnsNotVerified()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeFalse();
        result.Conflicts.ShouldHaveSingleItem();
        result.Conflicts[0].ConflictType.ShouldBe(DnsConflictType.ResolutionFailure);
    }

    // ── CNAME checks ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cname_MatchesExpected_ReturnsVerified()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("token", [new(DnsRecordType.Cname, "acme.com", "ingress.platform.example.com")]);

        SetupCname(dns, "acme.com", "ingress.platform.example.com.");

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeTrue();
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cname_Missing_ReturnsMissingCnameConflict()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("token", [new(DnsRecordType.Cname, "acme.com", "ingress.platform.example.com")]);

        SetupEmptyResponse(dns, "acme.com", QueryType.CNAME);

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeFalse();
        result.Conflicts.ShouldHaveSingleItem();
        result.Conflicts[0].ConflictType.ShouldBe(DnsConflictType.MissingCname);
    }

    [Fact]
    public async Task Cname_PointsToWrongTarget_ReturnsDivergentCnameConflict()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("token", [new(DnsRecordType.Cname, "acme.com", "ingress.platform.example.com")]);

        SetupCname(dns, "acme.com", "other-target.example.com.");

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeFalse();
        result.Conflicts.ShouldHaveSingleItem();
        result.Conflicts[0].ConflictType.ShouldBe(DnsConflictType.DivergentCname);
    }

    // ── TXT checks ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Txt_MatchesExpected_ReturnsVerified()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("tok", [new(DnsRecordType.Txt, "_granit-challenge.acme.com", "granit-verify=tok")]);

        SetupTxt(dns, "_granit-challenge.acme.com", "granit-verify=tok");

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeTrue();
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Txt_Missing_ReturnsMissingTxtConflict()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("tok", [new(DnsRecordType.Txt, "_granit-challenge.acme.com", "granit-verify=tok")]);

        SetupEmptyResponse(dns, "_granit-challenge.acme.com", QueryType.TXT);

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeFalse();
        result.Conflicts.ShouldHaveSingleItem();
        result.Conflicts[0].ConflictType.ShouldBe(DnsConflictType.MissingTxt);
    }

    // ── DNS failure ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DnsException_ReturnsResolutionFailureConflict()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("tok", [new(DnsRecordType.Cname, "acme.com", "ingress.platform.example.com")]);

        dns.QueryAsync("acme.com", QueryType.CNAME, cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(new DnsResponseException(DnsResponseCode.ServerFailure));

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeFalse();
        result.Conflicts.ShouldHaveSingleItem();
        result.Conflicts[0].ConflictType.ShouldBe(DnsConflictType.ResolutionFailure);
    }

    // ── Multiple records ─────────────────────────────────────────────────────

    [Fact]
    public async Task AllRecordsMatch_ReturnsVerified()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        ManagedHostname hostname = PendingHostname();

        SetupCname(dns, "acme.com", "ingress.platform.example.com.");
        SetupTxt(dns, "_granit-challenge.acme.com", "granit-verify=token123");

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeTrue();
        result.Conflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task OneRecordFails_ReturnsNotVerified_WithConflict()
    {
        ILookupClient dns = Substitute.For<ILookupClient>();
        var sut = new DnsHostnameVerifier(dns, NullLogger<DnsHostnameVerifier>.Instance);
        ManagedHostname hostname = PendingHostname();

        SetupCname(dns, "acme.com", "ingress.platform.example.com.");
        SetupEmptyResponse(dns, "_granit-challenge.acme.com", QueryType.TXT);

        HostnameVerificationResult result = await sut.VerifyAsync(hostname, TestContext.Current.CancellationToken);

        result.IsVerified.ShouldBeFalse();
        result.Conflicts.ShouldHaveSingleItem();
        result.Conflicts[0].ConflictType.ShouldBe(DnsConflictType.MissingTxt);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetupCname(ILookupClient dns, string name, string canonicalName)
    {
        var record = new CNameRecord(
            new ResourceRecordInfo(name, ResourceRecordType.CNAME, QueryClass.IN, 300, 0),
            DnsString.Parse(canonicalName));

        IDnsQueryResponse response = BuildResponse([record]);
        dns.QueryAsync(name, QueryType.CNAME, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(response);
    }

    private static void SetupTxt(ILookupClient dns, string name, params string[] values)
    {
        var record = new TxtRecord(
            new ResourceRecordInfo(name, ResourceRecordType.TXT, QueryClass.IN, 300, 0),
            values,
            values);

        IDnsQueryResponse response = BuildResponse([record]);
        dns.QueryAsync(name, QueryType.TXT, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(response);
    }

    private static void SetupEmptyResponse(ILookupClient dns, string name, QueryType queryType)
    {
        IDnsQueryResponse response = BuildResponse([]);
        dns.QueryAsync(name, queryType, cancellationToken: Arg.Any<CancellationToken>())
            .Returns(response);
    }

    private static IDnsQueryResponse BuildResponse(IReadOnlyList<DnsResourceRecord> answers)
    {
        IDnsQueryResponse response = Substitute.For<IDnsQueryResponse>();
        response.Answers.Returns(answers);
        return response;
    }
}
