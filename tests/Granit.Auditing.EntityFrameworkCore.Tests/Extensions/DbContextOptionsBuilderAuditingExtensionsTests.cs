// =============================================================================
// DbContextOptionsBuilderAuditingExtensionsTests - UseGranitAuditingInterceptor
// =============================================================================
// Verifies:
//   - Adds interceptor when registered in DI
//   - Does not throw when interceptor is not registered
//   - Throws on null options
//   - Throws on null serviceProvider
//   - Returns the options builder for chaining
// =============================================================================

using Granit.Auditing.EntityFrameworkCore.Extensions;
using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Auditing.EntityFrameworkCore.Tests.Extensions;

public sealed class DbContextOptionsBuilderAuditingExtensionsTests
{
    // -------------------------------------------------------------------------
    // UseGranitAuditingInterceptor — null guards
    // -------------------------------------------------------------------------

    [Fact]
    public void UseGranitAuditingInterceptor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceProvider sp = new ServiceCollection().BuildServiceProvider();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            DbContextOptionsBuilderAuditingExtensions.UseGranitAuditingInterceptor(null!, sp));
    }

    [Fact]
    public void UseGranitAuditingInterceptor_NullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        DbContextOptionsBuilder builder = new();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            builder.UseGranitAuditingInterceptor(null!));
    }

    // -------------------------------------------------------------------------
    // UseGranitAuditingInterceptor — with interceptor registered
    // -------------------------------------------------------------------------

    [Fact]
    public void UseGranitAuditingInterceptor_InterceptorRegistered_AddsToOptions()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        services.AddScoped<AuditingChangeTrackingInterceptor>();
        ServiceProvider sp = services.BuildServiceProvider();

        using IServiceScope scope = sp.CreateScope();
        DbContextOptionsBuilder builder = new();

        // Act
        DbContextOptionsBuilder result = builder.UseGranitAuditingInterceptor(scope.ServiceProvider);

        // Assert — returns the builder for chaining
        result.ShouldBeSameAs(builder);
    }

    // -------------------------------------------------------------------------
    // UseGranitAuditingInterceptor — without interceptor registered
    // -------------------------------------------------------------------------

    [Fact]
    public void UseGranitAuditingInterceptor_InterceptorNotRegistered_DoesNotThrow()
    {
        // Arrange
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        DbContextOptionsBuilder builder = new();

        // Act & Assert — should not throw
        DbContextOptionsBuilder result = builder.UseGranitAuditingInterceptor(sp);
        result.ShouldBeSameAs(builder);
    }

    // -------------------------------------------------------------------------
    // UseGranitAuditingInterceptor — chaining
    // -------------------------------------------------------------------------

    [Fact]
    public void UseGranitAuditingInterceptor_ReturnsSameBuilder_ForChaining()
    {
        // Arrange
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        DbContextOptionsBuilder builder = new DbContextOptionsBuilder()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());

        // Act
        DbContextOptionsBuilder result = builder.UseGranitAuditingInterceptor(sp);

        // Assert
        result.ShouldBeSameAs(builder);
    }
}
