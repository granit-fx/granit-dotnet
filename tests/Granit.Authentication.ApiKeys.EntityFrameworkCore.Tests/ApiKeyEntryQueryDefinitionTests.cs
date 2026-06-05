using System.Linq.Expressions;
using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Dtos;
using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.Authentication.ApiKeys.Queries;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies that <see cref="ApiKeyEntryQueryDefinition"/> — which replaces the former bespoke
/// <c>IApiKeyAdminStore.ListAsync</c> — is wired correctly and that its risky bits (the SQL
/// projection and the revocation quick filters) translate against the real EF model.
/// </summary>
public sealed class ApiKeyEntryQueryDefinitionTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly ApiKeyEntryQueryDefinition _definition = new();

    public void Dispose() => _factory.Dispose();

    // --- Configuration ---

    [Fact]
    public void Projects_to_summary_response_without_secret_or_collections()
    {
        _definition.GetProjectionType().ShouldBe(typeof(ApiKeyListItemResponse));

        // The projection record carries no secret material nor detail-level collections.
        string[] members = [.. typeof(ApiKeyListItemResponse)
            .GetProperties()
            .Select(p => p.Name)];
        members.ShouldNotContain(nameof(ApiKeyEntry.HashedKey));
        members.ShouldNotContain(nameof(ApiKeyEntry.Permissions));
        members.ShouldNotContain(nameof(ApiKeyEntry.AllowedCidrs));
    }

    [Fact]
    public void Defaults_to_newest_first()
        => _definition.GetDefaultSort().ShouldBe("-createdAt");

    [Fact]
    public void Defines_active_default_and_include_revoked_override_quick_filters()
    {
        IReadOnlyList<QuickFilterDescriptor> quickFilters = _definition.GetQuickFilters();

        QuickFilterDescriptor active = quickFilters.Single(f => f.Name == "active");
        active.IsDefault.ShouldBeTrue();

        QuickFilterDescriptor includeRevoked = quickFilters.Single(f => f.Name == "includeRevoked");
        includeRevoked.IsDefault.ShouldBeFalse();
    }

    // --- Execution against the real EF model (SQLite) ---

    [Fact]
    public async Task Projection_translates_to_sql_over_the_real_model()
    {
        await SeedAsync(CreateEntry("hash1", "Alpha", ApiKeyType.Secret, "live"));

        var projection = (Expression<Func<ApiKeyEntry, ApiKeyListItemResponse>>)
            _definition.GetProjectionExpression()!;

        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        List<ApiKeyListItemResponse> items = await db.ApiKeys
            .AsNoTracking()
            .Select(projection)
            .ToListAsync(TestContext.Current.CancellationToken);

        ApiKeyListItemResponse item = items.ShouldHaveSingleItem();
        item.Name.ShouldBe("Alpha");
        item.Type.ShouldBe(ApiKeyType.Secret);
        item.Environment.ShouldBe("live");
    }

    [Fact]
    public async Task Active_quick_filter_excludes_revoked_keys()
    {
        await SeedAsync(CreateEntry("hash1", "Active Key"));
        ApiKeyEntry revoked = CreateEntry("hash2", "Revoked Key");
        revoked.Revoke(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedAsync(revoked);

        List<ApiKeyEntry> result = await ApplyQuickFilterAsync("active");

        result.ShouldHaveSingleItem().Name.ShouldBe("Active Key");
    }

    [Fact]
    public async Task IncludeRevoked_quick_filter_returns_all_keys()
    {
        await SeedAsync(CreateEntry("hash1", "Active Key"));
        ApiKeyEntry revoked = CreateEntry("hash2", "Revoked Key");
        revoked.Revoke(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await SeedAsync(revoked);

        List<ApiKeyEntry> result = await ApplyQuickFilterAsync("includeRevoked");

        result.Count.ShouldBe(2);
    }

    // --- Helpers ---

    private async Task<List<ApiKeyEntry>> ApplyQuickFilterAsync(string name)
    {
        var predicate = (Expression<Func<ApiKeyEntry, bool>>)
            _definition.GetQuickFilters().Single(f => f.Name == name).Predicate;

        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        return await db.ApiKeys
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedAsync(ApiKeyEntry entry)
    {
        await using AuthenticationApiKeysDbContext db = _factory.CreateDbContext();
        db.ApiKeys.Add(entry);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ApiKeyEntry CreateEntry(
        string hash = "default_hash",
        string name = "Test Key",
        ApiKeyType type = ApiKeyType.Secret,
        string environment = "test")
    {
        var entry = ApiKeyEntry.Create(
            Guid.NewGuid(), name, type, environment, hash, "gk_test_sk_", "abcd");
        entry.UpdatePermissions(["Read"]);
        entry.CreatedBy = "test";
        return entry;
    }
}
