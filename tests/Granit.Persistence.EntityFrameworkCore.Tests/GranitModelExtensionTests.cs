using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

/// <summary>
/// Covers the <see cref="IGranitModelExtension"/> hook: a separate package can augment a Granit module's EF
/// model (here, add a shadow property to <see cref="Widget"/>) by registering an extension in DI —
/// <see cref="GranitDbContext"/> resolves the set from the application service provider and applies it at the
/// end of model building. With none registered, the model is untouched.
/// </summary>
public sealed class GranitModelExtensionTests : IAsyncLifetime
{
    private const string AugmentedColumn = "Augmented";

    private SqliteConnection _connection = null!;

    public async ValueTask InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private DbContextOptions<WidgetDbContext> BuildOptions(IServiceProvider? applicationServices)
    {
        DbContextOptionsBuilder<WidgetDbContext> builder = new DbContextOptionsBuilder<WidgetDbContext>()
            .UseSqlite(_connection)
            // Mirror the framework wiring so each distinct extension set gets its own cached model
            // (otherwise EF caches one model per context type and the tests bleed into each other).
            .ReplaceService<IModelCacheKeyFactory, GranitModelCacheKeyFactory>();

        if (applicationServices is not null)
        {
            builder.UseApplicationServiceProvider(applicationServices);
        }

        return builder.Options;
    }

    [Fact]
    public void RegisteredExtension_AugmentsTheModel()
    {
        ServiceProvider app = new ServiceCollection()
            .AddSingleton<IGranitModelExtension, AddShadowPropertyExtension>()
            .BuildServiceProvider();

        using WidgetDbContext context = new(BuildOptions(app), new FakeCurrentTenant());

        context.Model.FindEntityType(typeof(Widget))!
            .FindProperty(AugmentedColumn)
            .ShouldNotBeNull("the registered IGranitModelExtension must have added the shadow property");
    }

    [Fact]
    public void AddGranitModelExtension_RegistersSingleton_AndIsIdempotent()
    {
        ServiceProvider app = new ServiceCollection()
            .AddGranitModelExtension<AddShadowPropertyExtension>()
            .AddGranitModelExtension<AddShadowPropertyExtension>()
            .BuildServiceProvider();

        IGranitModelExtension[] resolved = [.. app.GetServices<IGranitModelExtension>()];

        resolved.ShouldHaveSingleItem().ShouldBeOfType<AddShadowPropertyExtension>();

        using IServiceScope scope = app.CreateScope();
        scope.ServiceProvider.GetRequiredService<IGranitModelExtension>()
            .ShouldBeSameAs(resolved[0], "the extension must be a singleton so the cached model stays valid");
    }

    [Fact]
    public void NoExtensions_LeavesModelUntouched()
    {
        // No application service provider at all — the hook must short-circuit gracefully.
        using WidgetDbContext context = new(BuildOptions(applicationServices: null), new FakeCurrentTenant());

        context.Model.FindEntityType(typeof(Widget))!
            .FindProperty(AugmentedColumn)
            .ShouldBeNull("with no extensions registered the model must be untouched");
    }

    [Fact]
    public void Extension_TargetingAnotherEntity_IsAGracefulNoOp()
    {
        ServiceProvider app = new ServiceCollection()
            .AddSingleton<IGranitModelExtension, TargetsUnknownEntityExtension>()
            .BuildServiceProvider();

        // The extension self-guards on an entity type absent from this context — must not throw.
        using WidgetDbContext context = new(BuildOptions(app), new FakeCurrentTenant());

        context.Model.FindEntityType(typeof(Widget))!
            .FindProperty(AugmentedColumn)
            .ShouldBeNull();
    }

    // ── Test doubles ──────────────────────────────────────────────────

    public sealed class Widget
    {
        public int Id { get; set; }
    }

    private sealed class AddShadowPropertyExtension : IGranitModelExtension
    {
        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
            // Entity-gate: only touch contexts whose model owns Widget.
            if (modelBuilder.Model.FindEntityType(typeof(Widget)) is null)
            {
                return;
            }

            modelBuilder.Entity<Widget>().Property<string>(AugmentedColumn);
        }
    }

    private sealed class TargetsUnknownEntityExtension : IGranitModelExtension
    {
        private sealed class NotInThisContext
        {
            public int Id { get; set; }
        }

        public void Apply(ModelBuilder modelBuilder, DbContext context)
        {
            if (modelBuilder.Model.FindEntityType(typeof(NotInThisContext)) is null)
            {
                return;
            }

            modelBuilder.Entity<NotInThisContext>().Property<string>(AugmentedColumn);
        }
    }

    private sealed class WidgetDbContext(
        DbContextOptions<WidgetDbContext> options,
        ICurrentTenant tenant,
        IDataFilter? dataFilter = null)
        : GranitDbContext(options, tenant, dataFilter)
    {
        public DbSet<Widget> Widgets { get; set; } = null!;

        protected override void OnGranitModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Widget>().HasKey(w => w.Id);
    }
}
