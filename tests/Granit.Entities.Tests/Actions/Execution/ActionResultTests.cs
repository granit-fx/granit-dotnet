using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using System.Text.Json;
using Xunit;

#pragma warning disable IDE0007, IDE0008

namespace Granit.Entities.Tests.Actions.Execution;

/// <summary>
/// Tests for <see cref="ActionResult"/> and <see cref="BulkActionResult"/>
/// factory methods and semantics.
/// </summary>
public sealed class ActionResultTests
{
    [Fact]
    public void ActionResult_Success_Creates_IsSuccessTrue()
    {
        // Act
        ActionResult result = ActionResult.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ActionResult_Failure_Creates_IsSuccessFalse()
    {
        // Act
        ActionResult result = ActionResult.Failure("Granit:Validation:Error");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Granit:Validation:Error", result.ErrorMessage);
    }

    [Fact]
    public void ActionResult_Failure_ThrowsOnNullKey()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ActionResult.Failure(null!));
    }

    [Fact]
    public void BulkActionResult_Success_NoFailures()
    {
        // Act
        BulkActionResult result = BulkActionResult.Success(5);

        // Assert
        Assert.Equal(5, result.AffectedCount);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void BulkActionResult_WithFailures_IncludesFailureDetails()
    {
        // Arrange
        BulkFailure f1 = new BulkFailure("entity-1", "Error A");
        BulkFailure f2 = new BulkFailure("entity-2", "Error B");

        // Act
        BulkActionResult result = BulkActionResult.WithFailures(3, f1, f2);

        // Assert
        Assert.Equal(3, result.AffectedCount);
        Assert.Equal(2, result.Failures.Count);
        Assert.Contains(f1, result.Failures);
        Assert.Contains(f2, result.Failures);
    }

    [Fact]
    public void BulkActionResult_WithFailures_ThrowsOnNegativeAffected()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BulkActionResult.WithFailures(-1, new BulkFailure("id", "error")));
    }

    [Fact]
    public void BulkFailure_StoresIdAndMessage()
    {
        // Act
        BulkFailure failure = new BulkFailure("entity-123", "Permission denied");

        // Assert
        Assert.Equal("entity-123", failure.EntityId);
        Assert.Equal("Permission denied", failure.ErrorMessage);
    }
}

/// <summary>
/// Tests for <see cref="EntityActionBuilder{TEntity}"/> ServerExecutor and
/// BulkExecutor extensions.
/// </summary>
public sealed class EntityActionBuilderExecutorExtensionTests
{
    private sealed class TestEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DummyExecutor : IEntityActionExecutor<TestEntity>
    {
        public Task<ActionResult> ExecuteAsync(TestEntity entity, JsonElement payload, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Success());
    }

    private sealed class DummyBulkExecutor : IBulkActionExecutor<TestEntity>
    {
        public Task<BulkActionResult> ExecuteBulkAsync(
            IReadOnlyList<TestEntity> entities,
            JsonElement payload,
            CancellationToken cancellationToken)
            => Task.FromResult(BulkActionResult.Success(entities.Count));
    }

    [Fact]
    public void ServerExecutor_SetsRequiresServerExecutionFlag()
    {
        // Arrange
        EntityActionBuilder<TestEntity> builder = new EntityActionBuilder<TestEntity>("testAction");

        // Act
        builder
            .ApiCall("POST", "/api/test/testAction")
            .DisplayKey("Test.Action")
            .ServerExecutor<DummyExecutor>();

        EntityActionDescriptor descriptor = builder.Build();

        // Assert
        Assert.True(descriptor.RequiresServerExecution);
        Assert.Equal(typeof(DummyExecutor), descriptor.ServerExecutorType);
        Assert.Null(descriptor.BulkExecutorType);
    }

    [Fact]
    public void BulkExecutor_RequiresServerExecutorToBeSet()
    {
        // Arrange
        EntityActionBuilder<TestEntity> builder = new EntityActionBuilder<TestEntity>("testAction");

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => builder
                .ApiCall("POST", "/api/test/testAction")
                .DisplayKey("Test.Action")
                .BulkExecutor<DummyBulkExecutor>());

        Assert.Contains("ServerExecutor", ex.Message);
    }

    [Fact]
    public void BulkExecutor_RegistersTypeAfterServerExecutor()
    {
        // Arrange
        EntityActionBuilder<TestEntity> builder = new EntityActionBuilder<TestEntity>("testAction");

        // Act
        builder
            .ApiCall("POST", "/api/test/testAction")
            .DisplayKey("Test.Action")
            .ServerExecutor<DummyExecutor>()
            .BulkExecutor<DummyBulkExecutor>();

        EntityActionDescriptor descriptor = builder.Build();

        // Assert
        Assert.True(descriptor.RequiresServerExecution);
        Assert.Equal(typeof(DummyExecutor), descriptor.ServerExecutorType);
        Assert.Equal(typeof(DummyBulkExecutor), descriptor.BulkExecutorType);
    }
}

/// <summary>
/// Integration-style tests for bulk action execution orchestration.
/// </summary>
public sealed class BulkActionIntegrationTests
{
    private sealed class TestEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private sealed class ToggleActiveExecutor : IEntityActionExecutor<TestEntity>
    {
        public Task<ActionResult> ExecuteAsync(TestEntity entity, JsonElement payload, CancellationToken cancellationToken)
        {
            // Simulate toggle logic
            entity.IsActive = !entity.IsActive;
            return Task.FromResult(ActionResult.Success());
        }
    }

    private sealed class FailingExecutor : IEntityActionExecutor<TestEntity>
    {
        public Task<ActionResult> ExecuteAsync(TestEntity entity, JsonElement payload, CancellationToken cancellationToken)
        {
            // Simulate failure on specific entity
            if (entity.Name == "should-fail")
            {
                return Task.FromResult(ActionResult.Failure("Granit:Validation:Failed"));
            }
            return Task.FromResult(ActionResult.Success());
        }
    }

    [Fact]
    public void ActionResult_Immutable_SupportsEquality()
    {
        // Act
        var result1 = ActionResult.Success();
        var result2 = ActionResult.Success();

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void BulkActionResult_SupportsPartialFailure()
    {
        // Arrange
        var failures = new[]
        {
            new BulkFailure("id-1", "Error 1"),
            new BulkFailure("id-2", "Error 2"),
        };

        // Act
        var result = BulkActionResult.WithFailures(8, failures);

        // Assert
        Assert.Equal(8, result.AffectedCount);
        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public void EntityActionBuilder_Fluent_ChainServerAndBulkExecutors()
    {
        // Arrange
        var builder = new EntityActionBuilder<TestEntity>("toggleActive");

        // Act
        builder
            .ApiCall("POST", "/api/test/toggleActive")
            .DisplayKey("Entity.Actions.ToggleActive")
            .Icon("toggle-on")
            .Order(10)
            .OnSelection()
            .ServerExecutor<ToggleActiveExecutor>();

        var descriptor = builder.Build();

        // Assert
        Assert.Equal("toggleActive", descriptor.Name);
        Assert.Equal(EntityActionKind.ApiCall, descriptor.Kind);
        Assert.True(descriptor.RequiresServerExecution);
        Assert.True(descriptor.ShowOnSelection);
        Assert.Equal(typeof(ToggleActiveExecutor), descriptor.ServerExecutorType);
    }
}
