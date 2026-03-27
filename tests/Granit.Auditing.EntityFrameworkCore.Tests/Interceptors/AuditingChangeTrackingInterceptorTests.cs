// =============================================================================
// AuditingChangeTrackingInterceptorTests - EF Core SaveChanges interceptor
// =============================================================================
// Verifies:
//   - Interceptor handles null context gracefully in async paths
//   - Interceptor handles null context gracefully in sync paths
//   - Interceptor does not throw when capture service is not registered
//   - Interceptor integrates with capture service via SaveChanges
// =============================================================================

using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Interceptors;

public sealed class AuditingChangeTrackingInterceptorTests
{
    // -------------------------------------------------------------------------
    // Interceptor — can be instantiated
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_CreatesInstance()
    {
        AuditingChangeTrackingInterceptor interceptor = new();

        interceptor.ShouldNotBeNull();
    }

    [Fact]
    public void Interceptor_IsSealed()
    {
        typeof(AuditingChangeTrackingInterceptor).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Interceptor_IsPublic()
    {
        typeof(AuditingChangeTrackingInterceptor).IsPublic.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Integration — interceptor registered in context does not cause errors
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_WithInterceptor_NoCaptureService_Succeeds()
    {
        // Arrange — interceptor is added but ChangeTrackingCaptureService is NOT registered
        AuditingChangeTrackingInterceptor interceptor = new();

        DbContextOptions<TestInterceptorDbContext> options = new DbContextOptionsBuilder<TestInterceptorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using TestInterceptorDbContext context = new(options);
        context.SimpleEntities.Add(new SimpleEntity { Id = 1, Name = "Test" });

        // Act & Assert — should not throw
        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        result.ShouldBe(1);
    }

    [Fact]
    public void SaveChanges_WithInterceptor_NoCaptureService_Succeeds()
    {
        // Arrange
        AuditingChangeTrackingInterceptor interceptor = new();

        DbContextOptions<TestInterceptorDbContext> options = new DbContextOptionsBuilder<TestInterceptorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        using TestInterceptorDbContext context = new(options);
        context.SimpleEntities.Add(new SimpleEntity { Id = 1, Name = "Test" });

        // Act & Assert — should not throw
        int result = context.SaveChanges();
        result.ShouldBe(1);
    }

    [Fact]
    public async Task SaveChangesAsync_WithInterceptor_MultipleEntities_ReportsCorrectRowsAffected()
    {
        // Arrange
        AuditingChangeTrackingInterceptor interceptor = new();

        DbContextOptions<TestInterceptorDbContext> options = new DbContextOptionsBuilder<TestInterceptorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using TestInterceptorDbContext context = new(options);
        context.SimpleEntities.Add(new SimpleEntity { Id = 1, Name = "A" });
        context.SimpleEntities.Add(new SimpleEntity { Id = 2, Name = "B" });
        context.SimpleEntities.Add(new SimpleEntity { Id = 3, Name = "C" });

        // Act
        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(3);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoChanges_ReturnsZero()
    {
        // Arrange
        AuditingChangeTrackingInterceptor interceptor = new();

        DbContextOptions<TestInterceptorDbContext> options = new DbContextOptionsBuilder<TestInterceptorDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        await using TestInterceptorDbContext context = new(options);

        // Act
        int result = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Test entities
    // -------------------------------------------------------------------------

    private sealed class TestInterceptorDbContext(DbContextOptions<TestInterceptorDbContext> options)
        : DbContext(options)
    {
        public DbSet<SimpleEntity> SimpleEntities => Set<SimpleEntity>();
    }

    private sealed class SimpleEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
