using Granit.Modularity;
using Granit.Workflow.Notifications.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Notifications.Tests;

public sealed class GranitWorkflowNotificationsModuleTests
{
    [Fact]
    public void Module_DependsOn_GranitWorkflowModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitWorkflowNotificationsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitWorkflowModule));
    }

    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitWorkflowNotificationsModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        typeof(GranitWorkflowNotificationsModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();
    }

    [Fact]
    public void ConfigureServices_RegistersApproverResolver()
    {
        GranitWorkflowNotificationsModule module = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);
        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        IApproverResolver resolver = sp.GetRequiredService<IApproverResolver>();
        resolver.ShouldBeOfType<NullApproverResolver>();
    }
}
