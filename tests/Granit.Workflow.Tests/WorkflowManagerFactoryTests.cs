using Granit.MultiTenancy;
using Granit.Workflow.Domain;
using Granit.Workflow.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for keyed workflow registration and <see cref="IWorkflowManagerFactory"/> — the core
/// scenario where two entities share the same <see cref="WorkflowLifecycleStatus"/> enum but
/// require distinct, permission-gated definitions.
/// </summary>
public sealed class WorkflowManagerFactoryTests
{
    private const string BlogPostType = "BlogPost";
    private const string CmsPageType = "CmsPage";

    // Two distinct definitions on the SAME TState, gating on different permissions.
    private static readonly WorkflowDefinition<WorkflowLifecycleStatus> BlogDefinition =
        WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
            .InitialState(WorkflowLifecycleStatus.Draft)
            .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published, t => t
                .Named("Publish")
                .RequiresPermission("Blog.Posts.Publish")));

    private static readonly WorkflowDefinition<WorkflowLifecycleStatus> PageDefinition =
        WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
            .InitialState(WorkflowLifecycleStatus.Draft)
            .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published, t => t
                .Named("Publish")
                .RequiresPermission("Cms.Pages.Publish")));

    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton(Substitute.For<ICurrentTenant>());
        services.AddGranitWorkflow();
        services.AddWorkflow(BlogPostType, BlogDefinition);
        services.AddWorkflow(CmsPageType, PageDefinition);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void For_SameTState_DifferentKeys_ShouldResolveIndependentDefinitions()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IWorkflowManagerFactory factory =
            scope.ServiceProvider.GetRequiredService<IWorkflowManagerFactory>();

        IWorkflowManager<WorkflowLifecycleStatus> blog = factory.GetManager<WorkflowLifecycleStatus>(BlogPostType);
        IWorkflowManager<WorkflowLifecycleStatus> page = factory.GetManager<WorkflowLifecycleStatus>(CmsPageType);

        blog.ShouldNotBeSameAs(page);
    }

    [Fact]
    public async Task For_SameTState_ShouldGateOnEachDefinitionsOwnPermission()
    {
        // A permission checker granting ONLY the blog permission proves the two managers
        // consult different definitions: blog completes, page is denied.
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton(Substitute.For<ICurrentTenant>());
        IWorkflowPermissionChecker checker = Substitute.For<IWorkflowPermissionChecker>();
        checker.IsGrantedAsync("Blog.Posts.Publish", Arg.Any<CancellationToken>()).Returns(true);
        checker.IsGrantedAsync("Cms.Pages.Publish", Arg.Any<CancellationToken>()).Returns(false);
        services.AddScoped(_ => checker);
        services.AddGranitWorkflow();
        services.AddWorkflow(BlogPostType, BlogDefinition);
        services.AddWorkflow(CmsPageType, PageDefinition);

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        IWorkflowManagerFactory factory =
            scope.ServiceProvider.GetRequiredService<IWorkflowManagerFactory>();

        TransitionResult<WorkflowLifecycleStatus> blogResult =
            await factory.GetManager<WorkflowLifecycleStatus>(BlogPostType).TransitionAsync(
                WorkflowLifecycleStatus.Draft,
                WorkflowLifecycleStatus.Published,
                cancellationToken: TestContext.Current.CancellationToken);

        TransitionResult<WorkflowLifecycleStatus> pageResult =
            await factory.GetManager<WorkflowLifecycleStatus>(CmsPageType).TransitionAsync(
                WorkflowLifecycleStatus.Draft,
                WorkflowLifecycleStatus.Published,
                cancellationToken: TestContext.Current.CancellationToken);

        blogResult.Outcome.ShouldBe(TransitionOutcome.Completed);
        pageResult.Outcome.ShouldBe(TransitionOutcome.Denied);
    }

    [Fact]
    public void ForTEntity_ShouldResolveByStaticWorkflowEntityType()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IWorkflowManagerFactory factory =
            scope.ServiceProvider.GetRequiredService<IWorkflowManagerFactory>();

        IWorkflowManager<WorkflowLifecycleStatus> byKey = factory.GetManager<WorkflowLifecycleStatus>(BlogPostType);
        IWorkflowManager<WorkflowLifecycleStatus> byEntity =
            factory.GetManager<BlogPostVersion, WorkflowLifecycleStatus>();

        // Both resolve against the same keyed registration (scoped → same scope instance).
        byEntity.ShouldBeSameAs(byKey);
    }

    [Fact]
    public void FromKeyedServices_ShouldResolveKeyedManager()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();

        IWorkflowManager<WorkflowLifecycleStatus> manager =
            scope.ServiceProvider.GetRequiredKeyedService<IWorkflowManager<WorkflowLifecycleStatus>>(BlogPostType);

        manager.ShouldNotBeNull();
    }

    [Fact]
    public void For_UnregisteredKey_ShouldThrow()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IWorkflowManagerFactory factory =
            scope.ServiceProvider.GetRequiredService<IWorkflowManagerFactory>();

        Should.Throw<InvalidOperationException>(() =>
            factory.GetManager<WorkflowLifecycleStatus>("Unknown"));
    }

    [Fact]
    public void For_NullOrEmptyKey_ShouldThrow()
    {
        using ServiceProvider sp = BuildProvider();
        using IServiceScope scope = sp.CreateScope();
        IWorkflowManagerFactory factory =
            scope.ServiceProvider.GetRequiredService<IWorkflowManagerFactory>();

        Should.Throw<ArgumentException>(() => factory.GetManager<WorkflowLifecycleStatus>(string.Empty));
    }

    [Fact]
    public void NonKeyedRegistration_ShouldBeReachableViaFactoryDefaultKey()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton(Substitute.For<ICurrentTenant>());
        services.AddGranitWorkflow();
        services.AddWorkflow(BlogDefinition); // non-keyed

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();
        IWorkflowManagerFactory factory =
            scope.ServiceProvider.GetRequiredService<IWorkflowManagerFactory>();

        IWorkflowManager<WorkflowLifecycleStatus> manager =
            factory.GetManager<WorkflowLifecycleStatus>(typeof(WorkflowLifecycleStatus).FullName!);

        manager.ShouldNotBeNull();
    }

    /// <summary>Minimal <see cref="IWorkflowStateful"/> stub keyed as <c>"BlogPost"</c>.</summary>
    private sealed class BlogPostVersion : IWorkflowStateful
    {
        public static string StatusPropertyName => "LifecycleStatus";
        public static string WorkflowEntityType => BlogPostType;
        public string GetWorkflowEntityId() => Guid.Empty.ToString();
        public void RaiseWorkflowStateChangedEvent(
            string entityType, string previousState, string newState, string transitionedBy)
        {
        }
    }
}
