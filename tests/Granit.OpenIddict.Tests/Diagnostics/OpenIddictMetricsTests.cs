using System.Diagnostics.Metrics;
using Granit.OpenIddict.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Diagnostics;

public sealed class OpenIddictMetricsTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly OpenIddictMetrics _metrics;

    public OpenIddictMetricsTests()
    {
        _serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();

        IMeterFactory meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
        _metrics = new OpenIddictMetrics(meterFactory);
    }

    [Fact]
    public void Constructor_DoesNotThrow_With_Valid_MeterFactory() =>
        _metrics.ShouldNotBeNull();

    [Fact]
    public void MeterName_Is_Granit_OpenIddict() =>
        OpenIddictMetrics.MeterName.ShouldBe("Granit.OpenIddict");

    [Fact]
    public void RecordTokenIssued_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordTokenIssued("tenant-1", "authorization_code"));

    [Fact]
    public void RecordTokenIssued_WithNullTenant_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordTokenIssued(null, "client_credentials"));

    [Fact]
    public void RecordTokenRevoked_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordTokenRevoked("tenant-1", "logout"));

    [Fact]
    public void RecordAuthenticationSuccess_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordAuthenticationSuccess("tenant-1", "password"));

    [Fact]
    public void RecordAuthenticationFailure_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordAuthenticationFailure("tenant-1", "invalid_credentials"));

    [Fact]
    public void RecordAuthenticationFailure_WithNullTenant_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordAuthenticationFailure(null, "invalid_credentials"));

    [Fact]
    public void RecordRegistration_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordRegistration("tenant-1"));

    [Fact]
    public void RecordRegistration_WithNullTenant_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordRegistration(null));

    [Fact]
    public void RecordPasswordChange_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordPasswordChange("tenant-1"));

    [Fact]
    public void RecordPasswordReset_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordPasswordReset("tenant-1"));

    [Fact]
    public void RecordAccountDeletion_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordAccountDeletion("tenant-1"));

    [Fact]
    public void RecordImpersonation_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordImpersonation("tenant-1"));

    [Fact]
    public void RecordTwoFactorEvent_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordTwoFactorEvent("tenant-1", "enable"));

    [Fact]
    public void RecordExternalLogin_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordExternalLogin("tenant-1", "Google", false));

    [Fact]
    public void RecordExternalLogin_NewUser_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordExternalLogin("tenant-1", "Microsoft", true));

    [Fact]
    public void RecordKeyRotation_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordKeyRotation("tenant-1", 1, 2, 0));

    [Fact]
    public void RecordTokenIssuanceDuration_DoesNotThrow() =>
        Should.NotThrow(() =>
            _metrics.RecordTokenIssuanceDuration("tenant-1", "authorization_code", TimeSpan.FromMilliseconds(150)));

    public void Dispose() =>
        _serviceProvider.Dispose();
}
