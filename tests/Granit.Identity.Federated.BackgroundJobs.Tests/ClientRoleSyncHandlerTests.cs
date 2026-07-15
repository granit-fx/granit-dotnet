using Granit.Identity.Federated.BackgroundJobs.Jobs;
using Granit.Identity.Federated.Sync;
using NSubstitute;
using Xunit;

namespace Granit.Identity.Federated.BackgroundJobs.Tests;

public sealed class ClientRoleSyncHandlerTests
{
    private static IClientRoleSyncPolicy Policy(string name)
    {
        IClientRoleSyncPolicy policy = Substitute.For<IClientRoleSyncPolicy>();
        policy.ProviderName.Returns(name);
        return policy;
    }

    [Fact]
    public async Task Handle_DrivesEveryRegisteredPolicyThroughTheEngine()
    {
        IClientRoleSyncEngine engine = Substitute.For<IClientRoleSyncEngine>();
        IClientRoleSyncPolicy keycloak = Policy("Keycloak");
        IClientRoleSyncPolicy cognito = Policy("Cognito");

        await ClientRoleSyncHandler.HandleAsync(
            new ClientRoleSyncJob(), engine, [keycloak, cognito], TestContext.Current.CancellationToken);

        await engine.Received(1).SyncAsync(keycloak, Arg.Any<CancellationToken>());
        await engine.Received(1).SyncAsync(cognito, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoProviders_IsNoOp()
    {
        IClientRoleSyncEngine engine = Substitute.For<IClientRoleSyncEngine>();

        await ClientRoleSyncHandler.HandleAsync(
            new ClientRoleSyncJob(), engine, [], TestContext.Current.CancellationToken);

        await engine.DidNotReceiveWithAnyArgs().SyncAsync(default!, TestContext.Current.CancellationToken);
    }
}
