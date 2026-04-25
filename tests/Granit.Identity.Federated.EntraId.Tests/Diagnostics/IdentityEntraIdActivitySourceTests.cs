using System.Diagnostics;
using Granit.Identity.Federated.EntraId.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests.Diagnostics;

[Collection("EntraIdActivitySource")]
public sealed class IdentityEntraIdActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public IdentityEntraIdActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == IdentityEntraIdActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_is_Granit_Identity_Federated_EntraId() =>
        IdentityEntraIdActivitySource.Name.ShouldBe("Granit.Identity.Federated.EntraId");

    [Fact]
    public void StartActivity_returns_activity_when_listener_attached()
    {
        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetUsers);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("identity.entraid.get-users");
    }
}
