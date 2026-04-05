using Granit.Templating.Enrichment;
using Granit.Templating.Extensions;
using Granit.Templating.GlobalContext;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using Granit.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    // -------------------------------------------------------------------------
    // AddGranitTemplating
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitTemplating_Registers_ITextTemplateRenderer_Scoped()
    {
        ServiceCollection services = [];
        services.AddGranitTemplating();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITextTemplateRenderer) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitTemplating_ITextTemplateRenderer_IsReplaceable_WithTryAdd()
    {
        ServiceCollection services = [];
        services.AddScoped<ITextTemplateRenderer>(_ => null!); // pre-register
        services.AddGranitTemplating();                         // TryAdd must not replace

        services.Count(d => d.ServiceType == typeof(ITextTemplateRenderer))
                .ShouldBe(1, "TryAddScoped must not add a duplicate");
    }

    [Fact]
    public void AddGranitTemplating_Registers_ITemplateTransitionHook_Singleton()
    {
        ServiceCollection services = [];
        services.AddGranitTemplating();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateTransitionHook) &&
            d.ImplementationType == typeof(NullTemplateTransitionHook) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitTemplating_ITemplateTransitionHook_IsReplaceable_WithTryAdd()
    {
        ServiceCollection services = [];
        services.AddSingleton<ITemplateTransitionHook>(_ => null!); // pre-register
        services.AddGranitTemplating();                              // TryAdd must not replace

        services.Count(d => d.ServiceType == typeof(ITemplateTransitionHook))
                .ShouldBe(1, "TryAddSingleton must not add a duplicate");
    }

    [Fact]
    public void AddGranitTemplating_ITemplateTransitionHook_CanBeReplaced()
    {
        ServiceCollection services = [];
        services.AddGranitTemplating();
        services.Replace(ServiceDescriptor.Singleton<ITemplateTransitionHook, FakeTransitionHook>());

        ServiceProvider provider = services.BuildServiceProvider();
        ITemplateTransitionHook hook = provider.GetRequiredService<ITemplateTransitionHook>();

        hook.ShouldBeOfType<FakeTransitionHook>();
    }

    // -------------------------------------------------------------------------
    // AddEmbeddedTemplates
    // -------------------------------------------------------------------------

    [Fact]
    public void AddEmbeddedTemplates_Registers_ITemplateResolver_Singleton()
    {
        ServiceCollection services = [];
        services.AddEmbeddedTemplates(typeof(ServiceCollectionExtensionsTests).Assembly);

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateResolver) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddEmbeddedTemplates_CalledTwice_RegistersBothResolvers()
    {
        ServiceCollection services = [];
        services.AddEmbeddedTemplates(typeof(ServiceCollectionExtensionsTests).Assembly);
        services.AddEmbeddedTemplates(typeof(object).Assembly);

        services.Count(d => d.ServiceType == typeof(ITemplateResolver))
                .ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // AddTemplateGlobalContext
    // -------------------------------------------------------------------------

    [Fact]
    public void AddTemplateGlobalContext_Registers_As_Singleton()
    {
        ServiceCollection services = [];
        services.AddTemplateGlobalContext<FakeGlobalContext>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateGlobalContext) &&
            d.ImplementationType == typeof(FakeGlobalContext) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }

    // -------------------------------------------------------------------------
    // AddTemplateDataEnricher
    // -------------------------------------------------------------------------

    [Fact]
    public void AddTemplateDataEnricher_Registers_As_Transient()
    {
        ServiceCollection services = [];
        services.AddTemplateDataEnricher<string, FakeEnricher>();

        services.ShouldContain(d =>
            d.ServiceType == typeof(ITemplateDataEnricher<string>) &&
            d.ImplementationType == typeof(FakeEnricher) &&
            d.Lifetime == ServiceLifetime.Transient);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class FakeGlobalContext : ITemplateGlobalContext
    {
        public string ContextName => "fake";
        public object Resolve() => new { };
    }

    private sealed class FakeEnricher : ITemplateDataEnricher<string>
    {
        public int Order => 0;
        public Task<string> EnrichAsync(string data, CancellationToken cancellationToken = default) =>
            Task.FromResult(data);
    }

    private sealed class FakeTransitionHook : ITemplateTransitionHook
    {
        public bool IsWorkflowEnabled => true;
        public Task<bool> CanTransitionAsync(WorkflowLifecycleStatus from, WorkflowLifecycleStatus to, CancellationToken cancellationToken) =>
            Task.FromResult(true);
        public Task OnTransitionedAsync(Guid revisionId, WorkflowLifecycleStatus from, WorkflowLifecycleStatus to, string userId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
