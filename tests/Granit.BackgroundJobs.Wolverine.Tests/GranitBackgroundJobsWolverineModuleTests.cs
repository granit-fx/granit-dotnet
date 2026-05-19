using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Wolverine.Internal;
using Granit.Modularity;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Wolverine.Tests;

public sealed class GranitBackgroundJobsWolverineModuleTests
{
    [Fact]
    public void DependsOn_declares_background_jobs_and_wolverine_modules()
    {
        // Arrange & Act
        DependsOnAttribute? attribute = typeof(GranitBackgroundJobsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitBackgroundJobsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void ConfigureServices_replaces_background_job_dispatcher()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<IBackgroundJobDispatcher, StubBackgroundJobDispatcher>();
        builder.Services.AddSingleton<IDeadLetterQueueInspector, StubDeadLetterQueueInspector>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitBackgroundJobsWolverineModule module = new();

        // Act
        module.ConfigureServices(context);

        // Assert
        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IBackgroundJobDispatcher));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(WolverineBackgroundJobDispatcher));
    }

    [Fact]
    public void ConfigureServices_replaces_dead_letter_queue_inspector()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<IBackgroundJobDispatcher, StubBackgroundJobDispatcher>();
        builder.Services.AddSingleton<IDeadLetterQueueInspector, StubDeadLetterQueueInspector>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitBackgroundJobsWolverineModule module = new();

        // Act
        module.ConfigureServices(context);

        // Assert
        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IDeadLetterQueueInspector));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(WolverineDeadLetterQueueInspector));
    }

    private sealed class StubBackgroundJobDispatcher : IBackgroundJobDispatcher
    {
        public Task PublishAsync(object message, IDictionary<string, string>? headers = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ScheduleAsync(object message, DateTimeOffset scheduledTime, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubDeadLetterQueueInspector : IDeadLetterQueueInspector
    {
        public Task<IReadOnlyDictionary<string, long>> GetCountsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, long>>(new Dictionary<string, long>());
    }
}
