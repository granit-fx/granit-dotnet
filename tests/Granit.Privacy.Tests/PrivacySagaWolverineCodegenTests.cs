using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.DataExport.Internal;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.LegalAgreements;
using Granit.Privacy.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Wolverine;
using Wolverine.Persistence.Sagas;
using Wolverine.Runtime;
using Wolverine.Runtime.Handlers;
using Xunit;

namespace Granit.Privacy.Tests;

// Regression coverage for the Wolverine SagaChain `*Async` suffix bug.
//
// The direct unit tests in PersonalDataExportSagaTests / PersonalDataDeletionSagaTests
// invoke saga methods by hand — they pass whether or not Wolverine's codegen actually
// wires the method into the generated chain. These tests boot a real Wolverine host
// and inspect `SagaChain.StartingCalls` / `ExistingCalls` (populated by
// SagaChain.findByNames). If a saga method is named with an `Async` suffix,
// findByNames silently drops it and these arrays come back empty — which is exactly
// what led to the production `ArgumentOutOfRangeException: You must define the saga id`
// crash on the showcase app.
public sealed class PrivacySagaWolverineCodegenTests
{
    [Fact]
    public async Task ExportSaga_start_chain_wires_Start_method()
    {
        using IHost host = await BuildHostAsync();
        SagaChain chain = GetSagaChain(host, typeof(PersonalDataRequestedEto));

        chain.StartingCalls.ShouldNotBeEmpty();
        chain.StartingCalls.ShouldContain(c => c.Method.Name == "Start");
    }

    [Fact]
    public async Task ExportSaga_prepared_fragment_chain_wires_Handle_method()
    {
        using IHost host = await BuildHostAsync();
        SagaChain chain = GetSagaChain(host, typeof(PersonalDataPreparedEto));

        chain.ExistingCalls.ShouldNotBeEmpty();
        chain.ExistingCalls.ShouldContain(c => c.Method.Name == "Handle");
    }

    [Fact]
    public async Task DeletionSaga_start_chain_wires_Start_method()
    {
        using IHost host = await BuildHostAsync();
        SagaChain chain = GetSagaChain(host, typeof(DeletionDeferredEto));

        chain.StartingCalls.ShouldNotBeEmpty();
        chain.StartingCalls.ShouldContain(c => c.Method.Name == "Start");
    }

    [Fact]
    public async Task DeletionSaga_cancel_chain_wires_Handle_method()
    {
        using IHost host = await BuildHostAsync();
        SagaChain chain = GetSagaChain(host, typeof(DeletionCancelledEto));

        chain.ExistingCalls.ShouldNotBeEmpty();
        chain.ExistingCalls.ShouldContain(c => c.Method.Name == "Handle");
    }

    [Fact]
    public async Task DeletionSaga_provider_ack_chain_wires_Handle_method()
    {
        using IHost host = await BuildHostAsync();
        SagaChain chain = GetSagaChain(host, typeof(PersonalDataDeletedEto));

        chain.ExistingCalls.ShouldNotBeEmpty();
        chain.ExistingCalls.ShouldContain(c => c.Method.Name == "Handle");
    }

    [Fact]
    public async Task DeletionSaga_ack_timeout_chain_wires_Handle_method()
    {
        using IHost host = await BuildHostAsync();
        SagaChain chain = GetSagaChain(host, typeof(DeletionAcknowledgementTimedOutEvent));

        chain.ExistingCalls.ShouldNotBeEmpty();
        chain.ExistingCalls.ShouldContain(c => c.Method.Name == "Handle");
    }

    private static Task<IHost> BuildHostAsync() =>
        Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IDataProviderRegistry>(new DataProviderRegistry());
                services.AddSingleton(Substitute.For<IPrivacyScopeResolver>());
                services.AddSingleton<ICurrentTenant>(NullTenantContext.Instance);
                services.AddSingleton(Substitute.For<IDeletionRequestTrackerWriter>());
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton(Substitute.For<ILegalDocumentRegistry>());
                services.AddMetrics();
                services.AddSingleton<PrivacyMetrics>();
                services.Configure<GranitPrivacyOptions>(_ => { });
            })
            .UseWolverine(opts => opts.ApplicationAssembly = typeof(PersonalDataExportSaga).Assembly)
            .StartAsync();

    private static SagaChain GetSagaChain(IHost host, Type messageType)
    {
        var runtime = (WolverineRuntime)host.Services.GetRequiredService<IWolverineRuntime>();

        // HandlerFor(...) forces the chain to be compiled (DetermineFrames runs,
        // populating SagaChain.StartingCalls / ExistingCalls). ChainFor(...) alone
        // returns the chain object with empty call arrays — so the assertion would
        // always fail without this step.
        runtime.Handlers.HandlerFor(messageType).ShouldNotBeNull(
            $"Wolverine could not compile a handler for {messageType.Name}.");

        HandlerChain? chain = runtime.Handlers.ChainFor(messageType);
        chain.ShouldNotBeNull($"Wolverine did not discover a handler chain for {messageType.Name}.");
        return chain.ShouldBeOfType<SagaChain>();
    }
}
