using Granit.Hostnames.Domain;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.Tests.Domain;

public sealed class ManagedHostnameTests
{
    private static readonly Guid Id = new("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OwnerId = new("66666666-6666-6666-6666-666666666666");
    private static readonly Guid TenantId = new("77777777-7777-7777-7777-777777777777");
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<ExpectedDnsRecord> SampleRecords =
    [
        new(DnsRecordType.Cname, "acme.com", "platform.granit.dev"),
        new(DnsRecordType.Txt, "_granit-challenge.acme.com", "granit-verify=abc123"),
    ];

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_sets_fields_and_starts_pending()
    {
        var hostname = ManagedHostname.Create(
            Id, "acme.com", "cms.site", OwnerId, TenantId, isPrimary: true);

        hostname.Id.ShouldBe(Id);
        hostname.Host.Value.ShouldBe("acme.com");
        hostname.OwnerType.ShouldBe("cms.site");
        hostname.OwnerId.ShouldBe(OwnerId);
        hostname.TenantId.ShouldBe(TenantId);
        hostname.IsPrimary.ShouldBeTrue();
        hostname.Status.ShouldBe(HostnameStatus.Pending);
    }

    [Fact]
    public void Create_defaults_tenant_to_null_and_not_primary()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        hostname.TenantId.ShouldBeNull();
        hostname.IsPrimary.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_owner_type(string ownerType)
    {
        Should.Throw<ArgumentException>(
            () => ManagedHostname.Create(Id, "acme.com", ownerType, OwnerId));
    }

    [Fact]
    public void Create_rejects_null_host()
    {
        Should.Throw<ArgumentNullException>(
            () => ManagedHostname.Create(Id, null!, "cms.site", OwnerId));
    }

    // ── SetPrimary / ClearPrimary ─────────────────────────────────────────────

    [Fact]
    public void SetPrimary_and_ClearPrimary_toggle_the_canonical_flag()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        hostname.SetPrimary();
        hostname.IsPrimary.ShouldBeTrue();

        hostname.ClearPrimary();
        hostname.IsPrimary.ShouldBeFalse();
    }

    // ── BeginVerification ─────────────────────────────────────────────────────

    [Fact]
    public void BeginVerification_transitions_to_verifying_and_sets_challenge()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        hostname.BeginVerification("token-abc", SampleRecords);

        hostname.Status.ShouldBe(HostnameStatus.Verifying);
        hostname.VerificationToken.ShouldBe("token-abc");
        hostname.ExpectedDnsRecords.Count.ShouldBe(2);
        hostname.FailedCheckCount.ShouldBe(0);
        hostname.NextCheckAt.ShouldBeNull();
    }

    [Fact]
    public void BeginVerification_resets_backoff()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("t1", SampleRecords);
        hostname.MarkFailed([], Now);
        hostname.FailedCheckCount.ShouldBe(1);

        hostname.BeginVerification("t2", SampleRecords);

        hostname.FailedCheckCount.ShouldBe(0);
        hostname.NextCheckAt.ShouldBeNull();
    }

    // ── MarkVerified ──────────────────────────────────────────────────────────

    [Fact]
    public void MarkVerified_transitions_to_active_and_clears_conflicts()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("token", SampleRecords);
        hostname.MarkFailed([new DnsConflict(DnsConflictType.MissingCname, "no cname")], Now);

        hostname.MarkVerified(Now);

        hostname.Status.ShouldBe(HostnameStatus.Active);
        hostname.LastCheckedAt.ShouldBe(Now);
        hostname.Conflicts.ShouldBeEmpty();
        hostname.FailedCheckCount.ShouldBe(0);
        hostname.NextCheckAt.ShouldBeNull();
    }

    [Fact]
    public void MarkVerified_raises_HostnameVerifiedEto()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId, TenantId);
        hostname.BeginVerification("t", SampleRecords);

        hostname.MarkVerified(Now);

        hostname.IntegrationEvents.ShouldContain(e => e is Granit.Hostnames.Domain.Events.HostnameVerifiedEto);
    }

    [Fact]
    public void MarkVerified_clears_verification_token_and_expected_records()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("secret-token", SampleRecords);

        hostname.MarkVerified(Now);

        hostname.VerificationToken.ShouldBeNull();
        hostname.ExpectedDnsRecords.ShouldBeEmpty();
    }

    // ── MarkFailed ────────────────────────────────────────────────────────────

    [Fact]
    public void MarkFailed_transitions_to_error_and_sets_backoff()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("t", SampleRecords);

        DnsConflict conflict = new(DnsConflictType.MissingCname, "no cname");
        hostname.MarkFailed([conflict], Now);

        hostname.Status.ShouldBe(HostnameStatus.Error);
        hostname.LastCheckedAt.ShouldBe(Now);
        hostname.Conflicts.ShouldContain(conflict);
        hostname.FailedCheckCount.ShouldBe(1);
        hostname.NextCheckAt.ShouldBe(Now.Add(TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData(1, 1)]    // 1 min
    [InlineData(2, 5)]    // 5 min
    [InlineData(3, 15)]   // 15 min
    [InlineData(4, 60)]   // 1 h
    [InlineData(5, 360)]  // 6 h
    [InlineData(6, 1440)] // 24 h cap
    [InlineData(10, 1440)] // still 24 h
    public void MarkFailed_backoff_follows_table(int consecutiveFailures, int expectedMinutes)
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("t", SampleRecords);

        for (int i = 0; i < consecutiveFailures; i++)
        {
            hostname.MarkFailed([], Now);
        }

        hostname.NextCheckAt.ShouldBe(Now.Add(TimeSpan.FromMinutes(expectedMinutes)));
    }

    [Fact]
    public void MarkFailed_beyond_dormancy_threshold_sets_next_check_null()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("t", SampleRecords);

        for (int i = 0; i < ManagedHostname.DormancyThreshold; i++)
        {
            hostname.MarkFailed([], Now);
        }

        hostname.NextCheckAt.ShouldBeNull();
        hostname.FailedCheckCount.ShouldBe(ManagedHostname.DormancyThreshold);
    }

    // ── RequestRecheck ────────────────────────────────────────────────────────

    [Fact]
    public void RequestRecheck_transitions_to_verifying_and_resets_backoff()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        hostname.BeginVerification("t", SampleRecords);
        hostname.MarkFailed([], Now);
        hostname.MarkFailed([], Now); // 2 failures → 5 min backoff

        hostname.RequestRecheck();

        hostname.Status.ShouldBe(HostnameStatus.Verifying);
        hostname.FailedCheckCount.ShouldBe(0);
        hostname.NextCheckAt.ShouldBeNull();
    }

    [Fact]
    public void RequestRecheck_works_from_error_and_active()
    {
        var h1 = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        h1.BeginVerification("t", SampleRecords);
        h1.MarkFailed([], Now);
        h1.RequestRecheck();
        h1.Status.ShouldBe(HostnameStatus.Verifying);

        var h2 = ManagedHostname.Create(Id, "acme2.com", "cms.site", OwnerId);
        h2.BeginVerification("t", SampleRecords);
        h2.MarkVerified(Now);
        h2.RequestRecheck();
        h2.Status.ShouldBe(HostnameStatus.Verifying);
    }

    // ── ReportCertificateStatus ───────────────────────────────────────────────

    [Fact]
    public void Create_defaults_certificate_status_to_unprovisioned()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        hostname.CertificateStatus.ShouldBe(CertificateStatus.Unprovisioned);
        hostname.CertExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void ReportCertificateStatus_Secured_sets_status_and_expiry()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);
        DateTimeOffset expires = Now.AddDays(90);

        hostname.ReportCertificateStatus(CertificateStatus.Secured, expires);

        hostname.CertificateStatus.ShouldBe(CertificateStatus.Secured);
        hostname.CertExpiresAt.ShouldBe(expires);
    }

    [Fact]
    public void ReportCertificateStatus_Secured_raises_HostnameCertificateSecuredEto()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId, TenantId);
        DateTimeOffset expires = Now.AddDays(90);

        hostname.ReportCertificateStatus(CertificateStatus.Secured, expires);

        Granit.Hostnames.Domain.Events.HostnameCertificateSecuredEto eto = hostname.IntegrationEvents
            .OfType<Granit.Hostnames.Domain.Events.HostnameCertificateSecuredEto>()
            .ShouldHaveSingleItem();
        eto.HostnameId.ShouldBe(Id);
        eto.Host.ShouldBe("acme.com");
        eto.ExpiresAt.ShouldBe(expires);
    }

    [Fact]
    public void ReportCertificateStatus_Error_clears_expiry_and_raises_HostnameCertificateFailedEto()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId, TenantId);
        hostname.ReportCertificateStatus(CertificateStatus.Secured, Now.AddDays(90));

        hostname.ReportCertificateStatus(CertificateStatus.Error);

        hostname.CertificateStatus.ShouldBe(CertificateStatus.Error);
        hostname.CertExpiresAt.ShouldBeNull();
        hostname.IntegrationEvents
            .OfType<Granit.Hostnames.Domain.Events.HostnameCertificateFailedEto>()
            .ShouldHaveSingleItem()
            .HostnameId.ShouldBe(Id);
    }

    [Fact]
    public void ReportCertificateStatus_Provisioning_raises_no_event()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId);

        hostname.ReportCertificateStatus(CertificateStatus.Provisioning);

        hostname.CertificateStatus.ShouldBe(CertificateStatus.Provisioning);
        hostname.CertExpiresAt.ShouldBeNull();
        hostname.IntegrationEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ReportCertificateStatus_is_idempotent_for_repeated_identical_status()
    {
        var hostname = ManagedHostname.Create(Id, "acme.com", "cms.site", OwnerId, TenantId);
        DateTimeOffset expires = Now.AddDays(90);

        hostname.ReportCertificateStatus(CertificateStatus.Secured, expires);
        hostname.ReportCertificateStatus(CertificateStatus.Secured, expires); // duplicate delivery

        // Only one SecuredEto should have been raised, not two.
        hostname.IntegrationEvents
            .OfType<Granit.Hostnames.Domain.Events.HostnameCertificateSecuredEto>()
            .ShouldHaveSingleItem();
    }
}
