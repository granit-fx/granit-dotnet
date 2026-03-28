using Granit.AI.Workspaces;
using Granit.Domain;

namespace Granit.AI.EntityFrameworkCore.Entities;

/// <summary>
/// EF Core entity for dynamic AI workspace configurations.
/// </summary>
internal sealed class AIWorkspaceEntity : AuditedEntity, IMultiTenant, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? SystemPrompt { get; set; }

    public float? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? TenantId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public AIWorkspace ToRecord() => new()
    {
        Name = Name,
        Provider = Provider,
        Model = Model,
        SystemPrompt = SystemPrompt,
        Temperature = Temperature,
        MaxOutputTokens = MaxOutputTokens,
        Kind = AIWorkspaceKind.Dynamic,
        TenantId = TenantId,
        IsActive = IsActive,
    };

    public static AIWorkspaceEntity FromRecord(AIWorkspace workspace) => new()
    {
        Name = workspace.Name,
        Provider = workspace.Provider,
        Model = workspace.Model,
        SystemPrompt = workspace.SystemPrompt,
        Temperature = workspace.Temperature,
        MaxOutputTokens = workspace.MaxOutputTokens,
        TenantId = workspace.TenantId,
        IsActive = workspace.IsActive,
    };
}
