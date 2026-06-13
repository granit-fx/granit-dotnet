using Granit.DataExchange.EntityFrameworkCore.Extensions;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Import.Domain;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: the query engine + analytics runner resolve <see cref="IQueryableSource{T}"/>
/// for <c>ImportJobQuery</c> / <c>ExportJobQuery</c>. Without the registrations the grids throw
/// "No service for type IQueryableSource&lt;T&gt;" at first request.
/// </summary>
public sealed class DataExchangeEfCoreQueryableSourceRegistrationTests
{
    [Fact]
    public void AddGranitDataExchangeEntityFrameworkCore_RegistersQueryableSource_ForImportJob_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitDataExchangeEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<ImportJob>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitDataExchangeEntityFrameworkCore_RegistersQueryableSource_ForExportJob_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitDataExchangeEntityFrameworkCore(_ => { });

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<ExportJob>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
