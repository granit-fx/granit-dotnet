using System.Diagnostics;
using Granit.Identity.Federated.Keycloak.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests.Diagnostics;

public sealed class IdentityKeycloakActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public IdentityKeycloakActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == IdentityKeycloakActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_is_Granit_Identity_Federated_Keycloak() =>
        IdentityKeycloakActivitySource.Name.ShouldBe("Granit.Identity.Federated.Keycloak");

    [Fact]
    public void StartActivity_returns_activity_when_listener_attached()
    {
        using Activity? activity = IdentityKeycloakActivitySource.Source.StartActivity(IdentityKeycloakActivitySource.GetUsers);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("identity.keycloak.get-users");
    }
}
