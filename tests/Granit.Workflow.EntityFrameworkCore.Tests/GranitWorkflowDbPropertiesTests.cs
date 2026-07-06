using Shouldly;
using Xunit;

namespace Granit.Workflow.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for <see cref="GranitWorkflowDbProperties"/> static properties.
/// </summary>
public sealed class GranitWorkflowDbPropertiesTests : IDisposable
{
    private readonly string _originalPrefix = GranitWorkflowDbProperties.DbTablePrefix;
    private readonly string? _originalSchema = GranitWorkflowDbProperties.DbSchema;

    public void Dispose()
    {
        // Restore original values to avoid test pollution
        GranitWorkflowDbProperties.DbTablePrefix = _originalPrefix;
        GranitWorkflowDbProperties.DbSchema = _originalSchema;
    }

    [Fact]
    public void DbTablePrefix_DefaultIsWorkflowUnderscore() =>
        GranitWorkflowDbProperties.DbTablePrefix.ShouldBe("workflow_");

    [Fact]
    public void DbSchema_DefaultIsNull() =>
        GranitWorkflowDbProperties.DbSchema.ShouldBeNull();

    [Fact]
    public void DbSchema_CanBeChanged()
    {
        // Act
        GranitWorkflowDbProperties.DbSchema = "app";

        // Assert
        GranitWorkflowDbProperties.DbSchema.ShouldBe("app");
    }
}
