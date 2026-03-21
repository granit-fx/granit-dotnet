using Granit.Timeline.Domain;
using Granit.Timeline.EntityFrameworkCore.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Timeline.EntityFrameworkCore.Tests;

public sealed class TimelineModelBuilderExtensionsTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TimelineModelBuilderExtensionsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void ConfigureTimelineModule_RegistersTimelineEntry()
    {
        DbContextOptionsBuilder<TestTimelineModelContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(_connection);

        using var context = new TestTimelineModelContext(optionsBuilder.Options);

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entryType =
            context.Model.FindEntityType(typeof(TimelineEntry));

        entryType.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureTimelineModule_RegistersTimelineAttachment()
    {
        DbContextOptionsBuilder<TestTimelineModelContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(_connection);

        using var context = new TestTimelineModelContext(optionsBuilder.Options);

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? attachmentType =
            context.Model.FindEntityType(typeof(TimelineAttachment));

        attachmentType.ShouldNotBeNull();
    }

    private sealed class TestTimelineModelContext(DbContextOptions<TestTimelineModelContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureTimelineModule();
        }
    }
}
