using Granit.QueryEngine;
using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: the query engine + analytics runner resolve <see cref="IQueryableSource{TimelineEntry}"/>
/// for <c>TimelineEntryQuery</c>. Without the registration the grid throws
/// "No service for type IQueryableSource&lt;TimelineEntry&gt;" at first request.
/// </summary>
public sealed class TimelineEfCoreQueryableSourceRegistrationTests
{
    [Fact]
    public void AddGranitTimelineEntityFrameworkCore_RegistersQueryableSource_ForTimelineEntry_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitTimelineEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<TimelineEntry>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
