using Granit.AI.Workspaces;
using Granit.DataProtection;
using Granit.Domain;
using Granit.Encryption;

namespace Granit.AI.EntityFrameworkCore.Entities;

/// <summary>
/// EF Core entity for dynamic AI workspace configurations.
/// </summary>
internal sealed class AIWorkspaceEntity : AuditedEntity, IActive, IMultiTenant, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? SystemPrompt { get; set; }

    public float? Temperature { get; set; }

    public int? MaxOutputTokens { get; set; }

    public bool Activated { get; set; } = true;

    public Guid? TenantId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    /// <summary>
    /// Workspace-scoped provider API key override. Encrypted at rest, omitted from audit
    /// trails, log fields, MCP responses, and exports. Mirrors <see cref="AIWorkspace.ApiKey"/>.
    /// </summary>
    [Encrypted]
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    public string? ApiKey { get; set; }

    /// <summary>
    /// Workspace-scoped endpoint override (URL). Not encrypted (URL, not a secret) but masked
    /// in audit/log/MCP output. Mirrors <see cref="AIWorkspace.Endpoint"/>.
    /// </summary>
    [SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Mask)]
    public string? Endpoint { get; set; }

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
        Activated = Activated,
        ApiKey = ApiKey,
        Endpoint = Endpoint,
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
        Activated = workspace.Activated,
        ApiKey = workspace.ApiKey,
        Endpoint = workspace.Endpoint,
    };
}
