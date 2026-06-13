using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: the query engine + analytics runner resolve
/// <see cref="IQueryableSource{BackgroundJobDefinition}"/> for <c>BackgroundJobDefinitionQuery</c>.
/// Without the registration the grid throws "No service for type
/// IQueryableSource&lt;BackgroundJobDefinition&gt;" at first request.
/// </summary>
public sealed class BackgroundJobsEfCoreQueryableSourceRegistrationTests
{
    [Fact]
    public void AddGranitBackgroundJobsEntityFrameworkCore_RegistersQueryableSource_ForBackgroundJobDefinition_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitBackgroundJobsEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<BackgroundJobDefinition>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
