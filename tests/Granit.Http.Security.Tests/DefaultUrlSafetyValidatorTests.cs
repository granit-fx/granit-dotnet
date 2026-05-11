using System.Net;
using Granit.Http.Security;
using Granit.Http.Security.Internal;
using Granit.Http.Security.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Security.Tests;

public sealed class DefaultUrlSafetyValidatorTests
{
    private static DefaultUrlSafetyValidator Create(
        FakeDnsResolver? resolver = null,
        UrlSafetyOptions? options = null,
        TimeProvider? clock = null)
    {
        options ??= new UrlSafetyOptions
        {
            AllowedSchemes = ["https", "http", "file"],
            DnsResolveTimeout = TimeSpan.FromSeconds(2),
        };
        resolver ??= FakeDnsResolver.Returning("8.8.8.8");
        return new DefaultUrlSafetyValidator(Microsoft.Extensions.Options.Options.Create(options), resolver, clock ?? TimeProvider.System);
    }

    // -------------------------------------------------------------------------
    // (1) Malformed URL
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RelativeUri_RejectedAsMalformed()
    {
        DefaultUrlSafetyValidator v = Create();

        UrlSafetyResult result = await v.ValidateAsync(new Uri("/relative", UriKind.Relative), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.MalformedUrl);
    }

    // -------------------------------------------------------------------------
    // (2) Length cap
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TooLongUrl_Rejected()
    {
        UrlSafetyOptions opts = new() { MaxUrlLength = 30, AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/" + new string('a', 100)), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.UrlTooLong);
    }

    // -------------------------------------------------------------------------
    // (3) Scheme allowlist
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("ftp://example.com/")]
    [InlineData("gopher://example.com/")]
    [InlineData("ldap://example.com/")]
    [InlineData("data:text/plain,foo")]
    public async Task DisallowedScheme_Rejected(string url)
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri(url), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.SchemeNotAllowed);
    }

    [Fact]
    public async Task HttpDisallowedWhenOnlyHttps()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("http://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.SchemeNotAllowed);
    }

    [Fact]
    public async Task SchemeMatchIsCaseInsensitive()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["HTTPS"] };
        DefaultUrlSafetyValidator v = Create(options: opts, resolver: FakeDnsResolver.Returning("8.8.8.8"));

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // (4) file:// short-circuit
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FileScheme_AllowedWhenInAllowlist_NoDnsCall()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["file"] };
        var resolver = FakeDnsResolver.Throws();
        DefaultUrlSafetyValidator v = Create(resolver, opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("file:///etc/passwd"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
        resolver.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task FileScheme_RejectedWhenNotInAllowlist()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("file:///etc/passwd"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.SchemeNotAllowed);
    }

    // -------------------------------------------------------------------------
    // (5) IDN normalization
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IdnHost_NormalizedAndResolved()
    {
        FakeDnsResolver resolver = new((host, _) =>
        {
            host.ShouldStartWith("xn--"); // Punycode-normalized.
            return ValueTask.FromResult<IPAddress[]>([IPAddress.Parse("93.184.216.34")]);
        });
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(resolver, opts);

        // Uri.Host already returns punycode for Unicode hosts; pass the original string.
        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://пример.рф/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // (6) Reserved TLDs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("https://printer.local/")]
    [InlineData("https://svc.internal/")]
    [InlineData("https://localhost/")]
    [InlineData("https://abc.onion/")]
    [InlineData("https://foo.test/")]
    [InlineData("https://anything.example/")]
    [InlineData("https://nothing.invalid/")]
    public async Task ReservedTld_Rejected(string url)
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri(url), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.ReservedTld);
    }

    // -------------------------------------------------------------------------
    // (7) Denied host patterns
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeniedHostPattern_Rejects()
    {
        UrlSafetyOptions opts = new()
        {
            AllowedSchemes = ["https"],
            DeniedHostPatterns = ["*.evil.com"],
        };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://api.evil.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.HostPatternDenied);
    }

    // -------------------------------------------------------------------------
    // (8) Allowed host patterns
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AllowedHostPattern_MissingMatch_Rejects()
    {
        UrlSafetyOptions opts = new()
        {
            AllowedSchemes = ["https"],
            AllowedHostPatterns = ["*.trusted.com"],
        };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://api.untrusted.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.HostPatternNotAllowed);
    }

    [Fact]
    public async Task AllowedHostPattern_MatchPasses()
    {
        UrlSafetyOptions opts = new()
        {
            AllowedSchemes = ["https"],
            AllowedHostPatterns = ["*.trusted.com"],
        };
        DefaultUrlSafetyValidator v = Create(
            resolver: FakeDnsResolver.Returning("93.184.216.34"),
            options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://api.trusted.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // (9 / 10) IP classification
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("http://127.0.0.1/", UrlSafetyViolationKind.Loopback)]
    [InlineData("http://10.0.0.1/", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("http://172.16.0.1/", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("http://192.168.1.1/", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("http://100.64.0.1/", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("http://0.0.0.0/", UrlSafetyViolationKind.PrivateNetwork)]
    [InlineData("http://169.254.0.1/", UrlSafetyViolationKind.LinkLocal)]
    [InlineData("http://169.254.169.254/", UrlSafetyViolationKind.MetadataEndpoint)]
    [InlineData("http://[::1]/", UrlSafetyViolationKind.Loopback)]
    [InlineData("http://[fe80::1]/", UrlSafetyViolationKind.LinkLocal)]
    [InlineData("http://[fc00::1]/", UrlSafetyViolationKind.IPv6UniqueLocal)]
    [InlineData("http://[fd00:ec2::254]/", UrlSafetyViolationKind.MetadataEndpoint)]
    public async Task LiteralBlockedIp_Rejected(string url, UrlSafetyViolationKind expected)
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["http"] };
        // IP literal — DNS resolver must not be called.
        var resolver = FakeDnsResolver.Throws();
        DefaultUrlSafetyValidator v = Create(resolver, opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri(url), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(expected);
        resolver.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task IPv4MappedIPv6_Loopback_Rejected()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["http"] };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("http://[::ffff:127.0.0.1]/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.Loopback);
    }

    [Fact]
    public async Task PublicHost_PassesAndReturnsAddresses()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(
            resolver: FakeDnsResolver.Returning("93.184.216.34", "8.8.8.8"),
            options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
        result.ResolvedAddresses.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DnsReturnsLoopback_Rejected_DnsRebindingDefense()
    {
        // Simulates DNS rebinding: hostname looked public but DNS responds with 127.0.0.1.
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(
            resolver: FakeDnsResolver.Returning("127.0.0.1"),
            options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.Loopback);
    }

    [Fact]
    public async Task DnsMixedReturn_OneBlocked_FailsClosed()
    {
        // Anti-rebinding stance: any blocked IP in the answer set fails the check.
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(
            resolver: FakeDnsResolver.Returning("93.184.216.34", "10.0.0.1"),
            options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.PrivateNetwork);
    }

    [Fact]
    public async Task AllowLoopback_PermitsLoopbackIp()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["http"], AllowLoopback = true };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("http://127.0.0.1/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task AllowPrivateNetworks_PermitsRfc1918()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["http"], AllowPrivateNetworks = true };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("http://10.1.2.3/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task AllowLoopback_DoesNotImplyMetadataEndpoint()
    {
        UrlSafetyOptions opts = new()
        {
            AllowedSchemes = ["http"],
            AllowLoopback = true,
            AllowPrivateNetworks = true,
        };
        DefaultUrlSafetyValidator v = Create(options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("http://169.254.169.254/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.MetadataEndpoint);
    }

    // -------------------------------------------------------------------------
    // DNS failure paths
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DnsSocketException_ReturnsDnsResolutionFailed()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(resolver: FakeDnsResolver.Throws(), options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.DnsResolutionFailed);
    }

    [Fact]
    public async Task DnsEmpty_ReturnsDnsResolutionFailed()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(resolver: FakeDnsResolver.Empty(), options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.DnsResolutionFailed);
    }

    [Fact]
    public async Task DnsTimeout_ReturnsDnsResolutionFailed()
    {
        UrlSafetyOptions opts = new()
        {
            AllowedSchemes = ["https"],
            DnsResolveTimeout = TimeSpan.FromMilliseconds(50),
        };
        DefaultUrlSafetyValidator v = Create(resolver: FakeDnsResolver.Hangs(), options: opts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("https://example.com/"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.Violation!.Kind.ShouldBe(UrlSafetyViolationKind.DnsResolutionFailed);
    }

    [Fact]
    public async Task ExternalCancellation_Propagates()
    {
        UrlSafetyOptions opts = new() { AllowedSchemes = ["https"] };
        DefaultUrlSafetyValidator v = Create(resolver: FakeDnsResolver.Hangs(), options: opts);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await v.ValidateAsync(new Uri("https://example.com/"), cts.Token));
    }

    // -------------------------------------------------------------------------
    // Misc
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NullUrl_Throws()
    {
        DefaultUrlSafetyValidator v = Create();
        await Should.ThrowAsync<ArgumentNullException>(async () => await v.ValidateAsync(null!));
    }

    [Fact]
    public async Task OverridesAreHonored()
    {
        UrlSafetyOptions globalOpts = new() { AllowedSchemes = ["https"] };
        UrlSafetyOptions localOpts = new() { AllowedSchemes = ["http"] };
        DefaultUrlSafetyValidator v = Create(
            resolver: FakeDnsResolver.Returning("8.8.8.8"),
            options: globalOpts);

        UrlSafetyResult result = await v.ValidateAsync(new Uri("http://example.com/"), localOpts, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }
}
