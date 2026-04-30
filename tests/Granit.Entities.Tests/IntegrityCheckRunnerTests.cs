// =============================================================================
// Tests - IntegrityCheckRunner (Throw / Warn / Off)
// =============================================================================

using Granit.Entities.Extensions;
using Granit.Entities.Internal;
using Granit.Entities.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Entities.Tests;

public sealed class IntegrityCheckRunnerTests
{
    [Fact]
    public async Task Throw_ThrowsOnUnresolvedReference()
    {
        using ServiceProvider provider = BuildProvider(IntegrityCheckMode.Throw, registerCitedQuery: false);
        IntegrityCheckRunner runner = ResolveRunner(provider);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => runner.StartAsync(CancellationToken.None));

        ex.Message.ShouldContain("integrity check failed");
        ex.Message.ShouldContain(typeof(SampleQueryDefinition).FullName!);
    }

    [Fact]
    public async Task Throw_PassesWhenAllReferencesResolved()
    {
        using ServiceProvider provider = BuildProvider(IntegrityCheckMode.Throw, registerCitedQuery: true);
        IntegrityCheckRunner runner = ResolveRunner(provider);

        await Should.NotThrowAsync(() => runner.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Warn_DoesNotThrow_OnUnresolvedReference()
    {
        using ServiceProvider provider = BuildProvider(IntegrityCheckMode.Warn, registerCitedQuery: false);
        IntegrityCheckRunner runner = ResolveRunner(provider);

        await Should.NotThrowAsync(() => runner.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Off_SkipsCheckEntirely()
    {
        using ServiceProvider provider = BuildProvider(IntegrityCheckMode.Off, registerCitedQuery: false);
        IntegrityCheckRunner runner = ResolveRunner(provider);

        await Should.NotThrowAsync(() => runner.StartAsync(CancellationToken.None));
    }

    private static ServiceProvider BuildProvider(IntegrityCheckMode mode, bool registerCitedQuery)
    {
        ServiceCollection services = new();
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddGranitEntities(opt => opt.IntegrityCheck = mode);
        services.AddEntityDefinition<SampleEntity, SampleEntityDefinition>();

        if (registerCitedQuery)
        {
            services.AddSingleton<SampleQueryDefinition>();
        }

        return services.BuildServiceProvider();
    }

    private static IntegrityCheckRunner ResolveRunner(IServiceProvider provider)
    {
        IEnumerable<IHostedService> hostedServices = provider.GetServices<IHostedService>();
        return hostedServices.OfType<IntegrityCheckRunner>().Single();
    }

    private sealed class SampleEntity;
    private sealed class SampleQueryDefinition;

    private sealed class SampleEntityDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.SampleEntity";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> b) =>
            b.Query<SampleQueryDefinition>();
    }
}
