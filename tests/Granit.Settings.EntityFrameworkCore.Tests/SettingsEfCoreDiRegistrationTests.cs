using Granit.QueryEngine;
using Granit.Settings.Domain;
using Granit.Settings.EntityFrameworkCore.Extensions;
using Granit.Settings.Values;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Settings.EntityFrameworkCore.Tests;

public sealed class SettingsEfCoreDiRegistrationTests
{
    // -------------------------------------------------------------------------
    // AddGranitSettingsEntityFrameworkCore
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitSettingsEntityFrameworkCore_RegistersISettingStoreReader_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitSettingsEntityFrameworkCore(
            opts => opts.Configure = db => db.UseInMemoryDatabase("di-test"));

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(ISettingStoreReader) &&
            d.Lifetime == ServiceLifetime.Scoped,
            "EfCoreSettingStore must be registered as Scoped for ISettingStoreReader");
    }

    [Fact]
    public void AddGranitSettingsEntityFrameworkCore_RegistersISettingStoreWriter_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitSettingsEntityFrameworkCore(
            opts => opts.Configure = db => db.UseInMemoryDatabase("di-test-w"));

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(ISettingStoreWriter) &&
            d.Lifetime == ServiceLifetime.Scoped,
            "EfCoreSettingStore must be registered as Scoped for ISettingStoreWriter");
    }

    [Fact]
    public void AddGranitSettingsEntityFrameworkCore_RegistersQueryableSource_ForSettingRecord_AsScoped()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitSettingsEntityFrameworkCore(
            opts => opts.Configure = db => db.UseInMemoryDatabase("di-test-qs"));

        // Backs MapGranitQuery<SettingRecord> / the analytics runner over SettingRecordQuery.
        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<SettingRecord>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitSettingsEntityFrameworkCore_ReturnsBuilder_ForChaining()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        IHostApplicationBuilder result = builder.AddGranitSettingsEntityFrameworkCore(
            opts => opts.Configure = db => db.UseInMemoryDatabase("di-test-chain"));

        result.ShouldBeSameAs(builder);
    }
}
