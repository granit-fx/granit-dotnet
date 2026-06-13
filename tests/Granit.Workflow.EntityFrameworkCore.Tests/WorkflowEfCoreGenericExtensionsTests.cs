using Granit.QueryEngine;
using Granit.Workflow.Domain;
using Granit.Workflow.EntityFrameworkCore.Extensions;
using Granit.Workflow.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Workflow.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for <see cref="WorkflowEfCoreServiceCollectionExtensions.AddGranitWorkflowEntityFrameworkCore{TDbContext}"/>.
/// </summary>
public sealed class WorkflowEfCoreGenericExtensionsTests
{
    [Fact]
    public void AddGranitWorkflowEntityFrameworkCore_Generic_ShouldRegisterInterceptor()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitWorkflowEntityFrameworkCore<TestWorkflowDbContext>();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(WorkflowTransitionInterceptor));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWorkflowEntityFrameworkCore_Generic_ShouldRegisterHistoryQuery()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitWorkflowEntityFrameworkCore<TestWorkflowDbContext>();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IWorkflowHistoryQuery));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWorkflowEntityFrameworkCore_Generic_ShouldRegisterTransitionRecorder()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitWorkflowEntityFrameworkCore<TestWorkflowDbContext>();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IWorkflowTransitionRecorder));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWorkflowEntityFrameworkCore_Generic_ShouldRegisterQueryableSource()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitWorkflowEntityFrameworkCore<TestWorkflowDbContext>();

        // Assert — backs MapGranitQuery<WorkflowTransitionRecord> + the analytics runner.
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IQueryableSource<WorkflowTransitionRecord>));
        descriptor.ShouldNotBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitWorkflowEntityFrameworkCore_Generic_ShouldReturnSameServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddGranitWorkflowEntityFrameworkCore<TestWorkflowDbContext>();

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitWorkflowEntityFrameworkCore_Generic_ShouldNotOverrideExistingRegistrations()
    {
        // Arrange
        ServiceCollection services = new();

        // Act — register twice
        services.AddGranitWorkflowEntityFrameworkCore<TestWorkflowDbContext>();
        services.AddGranitWorkflowEntityFrameworkCore<TestWorkflowDbContext>();

        // Assert — TryAddScoped prevents duplicates
        int interceptorCount = services.Count(d => d.ServiceType == typeof(WorkflowTransitionInterceptor));
        int queryCount = services.Count(d => d.ServiceType == typeof(IWorkflowHistoryQuery));
        int recorderCount = services.Count(d => d.ServiceType == typeof(IWorkflowTransitionRecorder));

        interceptorCount.ShouldBe(1);
        queryCount.ShouldBe(1);
        recorderCount.ShouldBe(1);
    }

    // ========================================================================
    // Test DbContext
    // ========================================================================

    private sealed class TestWorkflowDbContext(DbContextOptions<TestWorkflowDbContext> options)
        : DbContext(options), IWorkflowDbContext
    {
        public DbSet<WorkflowTransitionRecord> WorkflowTransitionRecords => Set<WorkflowTransitionRecord>();
    }
}
