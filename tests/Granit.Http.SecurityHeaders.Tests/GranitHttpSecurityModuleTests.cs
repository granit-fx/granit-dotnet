using Granit.Http.SecurityHeaders.Extensions;
using Granit.Http.SecurityHeaders.Options;
using Granit.Modularity;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

public sealed class GranitHttpSecurityModuleTests
{
    [Fact]
    public void GranitHttpSecurityModule_IsGranitModule() =>
        typeof(GranitHttpSecurityModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitHttpSecurityModule_IsSealed() =>
        typeof(GranitHttpSecurityModule).IsSealed.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Kestrel Server header
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitHttpSecurity_SuppressesKestrelServerHeader()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();
        KestrelServerOptions options =
            host.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        options.AddServerHeader.ShouldBeFalse(
            "Server header must be suppressed to prevent fingerprinting (OWASP, CWE-200)");
    }

    [Fact]
    public void AddGranitHttpSecurity_RespectsDisabledSuppression()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["SecurityHeaders:SuppressServerHeader"] = "false";

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();
        KestrelServerOptions options =
            host.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        options.AddServerHeader.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // HSTS
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitHttpSecurity_ConfiguresHsts_DefaultOneYear()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();
        HstsOptions hsts =
            host.Services.GetRequiredService<IOptions<HstsOptions>>().Value;

        hsts.MaxAge.ShouldBe(TimeSpan.FromSeconds(31_536_000));
        hsts.IncludeSubDomains.ShouldBeTrue();
        hsts.Preload.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitHttpSecurity_ConfiguresHsts_CustomValues()
    {
        // Use 2 years — above the 6-month minimum enforced by the validator
        // (VULN-206) and distinct from the 1-year default so the test still
        // proves the override applied.
        const int TwoYearsSeconds = 63_072_000;
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["SecurityHeaders:HstsMaxAgeSeconds"] = TwoYearsSeconds.ToString();
        builder.Configuration["SecurityHeaders:HstsIncludeSubDomains"] = "false";
        builder.Configuration["SecurityHeaders:HstsPreload"] = "true";

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();
        HstsOptions hsts =
            host.Services.GetRequiredService<IOptions<HstsOptions>>().Value;

        hsts.MaxAge.ShouldBe(TimeSpan.FromSeconds(TwoYearsSeconds));
        hsts.IncludeSubDomains.ShouldBeFalse();
        hsts.Preload.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitHttpSecurity_HstsDisabled_DoesNotOverrideDefaults()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["SecurityHeaders:EnableHsts"] = "false";

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();
        HstsOptions hsts =
            host.Services.GetRequiredService<IOptions<HstsOptions>>().Value;

        // When HSTS is disabled, ConfigureHstsOptions returns early,
        // leaving ASP.NET Core defaults (30 days, no subdomains)
        hsts.MaxAge.ShouldBe(TimeSpan.FromDays(30));
        hsts.IncludeSubDomains.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Options registration
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitHttpSecurity_RegistersSecurityHeadersOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();
        GranitSecurityHeadersOptions options =
            host.Services.GetRequiredService<IOptions<GranitSecurityHeadersOptions>>().Value;

        options.ShouldNotBeNull();
        options.SuppressServerHeader.ShouldBeTrue();
        options.EnableContentTypeOptions.ShouldBeTrue();
        options.XFrameOptions.ShouldBe("DENY");
    }

    [Fact]
    public void AddGranitHttpSecurity_BindsFromConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["SecurityHeaders:XFrameOptions"] = "SAMEORIGIN";
        builder.Configuration["SecurityHeaders:ReferrerPolicy"] = "no-referrer";

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();
        GranitSecurityHeadersOptions options =
            host.Services.GetRequiredService<IOptions<GranitSecurityHeadersOptions>>().Value;

        options.XFrameOptions.ShouldBe("SAMEORIGIN");
        options.ReferrerPolicy.ShouldBe("no-referrer");
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitHttpSecurity_RejectsInvalidXFrameOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["SecurityHeaders:XFrameOptions"] = "INVALID";

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();

        Should.Throw<OptionsValidationException>(() =>
            host.Services.GetRequiredService<IOptions<GranitSecurityHeadersOptions>>().Value);
    }

    [Fact]
    public void AddGranitHttpSecurity_RejectsNegativeHstsMaxAge()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["SecurityHeaders:HstsMaxAgeSeconds"] = "-1";

        builder.AddGranitHttpSecurity();

        using IHost host = builder.Build();

        Should.Throw<OptionsValidationException>(() =>
            host.Services.GetRequiredService<IOptions<GranitSecurityHeadersOptions>>().Value);
    }
}
