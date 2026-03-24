using Granit.Domain;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// A simple entity used for testing the import pipeline.
/// </summary>
internal sealed class TestEntity : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Niss { get; set; }
    public string? ExternalId { get; set; }
}
