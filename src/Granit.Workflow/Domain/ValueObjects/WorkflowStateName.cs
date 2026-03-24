using Granit.Domain;

namespace Granit.Workflow.Domain.ValueObjects;

/// <summary>
/// A workflow state name (e.g. <c>Draft</c>, <c>Published</c>, <c>Archived</c>).
/// Maximum 100 characters.
/// </summary>
public sealed class WorkflowStateName : SingleValueObject<string>
{
    private const int MaxLength = 100;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="WorkflowStateName"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty or exceeds 100 characters.
    /// </exception>
    public static WorkflowStateName Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Workflow state name exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        return new WorkflowStateName { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(WorkflowStateName name) => name.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator WorkflowStateName(string value) => Create(value);
}
