using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Granit.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Hostnames.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: the query engine + analytics runner resolve
/// <see cref="IQueryableSource{ManagedHostname}"/> for <c>ManagedHostnameQuery</c>. The same
/// registration call also wires <see cref="IQueryableSource{WorkflowTransitionRecord}"/> via
/// <c>AddGranitWorkflowEntityFrameworkCore&lt;HostnamesDbContext&gt;</c>.
/// </summary>
public sealed class HostnamesEfCoreQueryableSourceRegistrationTests
{
    [Fact]
    public void AddGranitHostnamesEntityFrameworkCore_RegistersQueryableSource_ForManagedHostname_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitHostnamesEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<ManagedHostname>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitHostnamesEntityFrameworkCore_RegistersQueryableSource_ForWorkflowTransitionRecord_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitHostnamesEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<WorkflowTransitionRecord>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
