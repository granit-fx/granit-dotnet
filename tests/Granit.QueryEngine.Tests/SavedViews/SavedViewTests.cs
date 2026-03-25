using Granit.QueryEngine.SavedViews;
using Granit.QueryEngine.SavedViews.Domain;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests.SavedViews;

public sealed class SavedViewTests
{
    [Fact]
    public void Required_properties_are_set()
    {
        SavedView view = new()
        {
            EntityType = "Acme.Patients",
            Name = "Active patients",
            UserId = "user-1",
        };

        view.EntityType.ShouldBe("Acme.Patients");
        view.Name.ShouldBe("Active patients");
        view.UserId.ShouldBe("user-1");
    }

    [Fact]
    public void Defaults_are_correct()
    {
        SavedView view = new()
        {
            EntityType = "Test",
            Name = "Test",
            UserId = "user-1",
        };

        view.IsShared.ShouldBeFalse();
        view.IsDefault.ShouldBeFalse();
        view.FilterJson.ShouldBeNull();
        view.SortJson.ShouldBeNull();
        view.GroupByJson.ShouldBeNull();
        view.VisibleColumnsJson.ShouldBeNull();
        view.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Optional_properties_can_be_set()
    {
        var tenantId = Guid.NewGuid();
        SavedView view = new()
        {
            EntityType = "Acme.Patients",
            Name = "My view",
            UserId = "user-1",
            IsShared = true,
            IsDefault = true,
            FilterJson = """{"name.contains":"Alice"}""",
            SortJson = """["-createdAt"]""",
            GroupByJson = """{"field":"status"}""",
            VisibleColumnsJson = """["name","email"]""",
            TenantId = tenantId,
        };

        view.IsShared.ShouldBeTrue();
        view.IsDefault.ShouldBeTrue();
        view.FilterJson.ShouldNotBeNull();
        view.SortJson.ShouldNotBeNull();
        view.GroupByJson.ShouldNotBeNull();
        view.VisibleColumnsJson.ShouldNotBeNull();
        view.TenantId.ShouldBe(tenantId);
    }
}

public sealed class NullSavedViewStoreTests
{
    [Fact]
    public async Task GetListAsync_throws_NotImplementedException()
    {
        NullSavedViewStore store = new();

        NotImplementedException exception = await Should.ThrowAsync<NotImplementedException>(
            () => store.GetListAsync("Test", "user-1", null, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Granit.QueryEngine.EntityFrameworkCore");
    }

    [Fact]
    public async Task GetAsync_throws_NotImplementedException()
    {
        NullSavedViewStore store = new();

        await Should.ThrowAsync<NotImplementedException>(
            () => store.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_throws_NotImplementedException()
    {
        NullSavedViewStore store = new();
        SavedView view = new() { EntityType = "T", Name = "V", UserId = "U" };

        await Should.ThrowAsync<NotImplementedException>(
            () => store.CreateAsync(view, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_throws_NotImplementedException()
    {
        NullSavedViewStore store = new();
        SavedView view = new() { EntityType = "T", Name = "V", UserId = "U" };

        await Should.ThrowAsync<NotImplementedException>(
            () => store.UpdateAsync(view, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_throws_NotImplementedException()
    {
        NullSavedViewStore store = new();

        await Should.ThrowAsync<NotImplementedException>(
            () => store.DeleteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetDefaultAsync_throws_NotImplementedException()
    {
        NullSavedViewStore store = new();

        await Should.ThrowAsync<NotImplementedException>(
            () => store.SetDefaultAsync(Guid.NewGuid(), "user-1", "Test", TestContext.Current.CancellationToken));
    }
}

public sealed class SavedViewSummaryTests
{
    [Fact]
    public void Properties_are_preserved()
    {
        var id = Guid.NewGuid();
        SavedViewSummary dto = new(id, "My view", true, false);

        dto.Id.ShouldBe(id);
        dto.Name.ShouldBe("My view");
        dto.IsShared.ShouldBeTrue();
        dto.IsDefault.ShouldBeFalse();
    }
}

public sealed class CreateSavedViewRequestTests
{
    [Fact]
    public void Required_properties_are_set()
    {
        CreateSavedViewRequest request = new()
        {
            Name = "Active patients",
            FilterJson = """{"status.eq":"Active"}""",
        };

        request.Name.ShouldBe("Active patients");
        request.FilterJson.ShouldNotBeNull();
        request.IsShared.ShouldBeFalse();
        request.IsDefault.ShouldBeFalse();
    }
}

public sealed class UpdateSavedViewRequestTests
{
    [Fact]
    public void Required_properties_are_set()
    {
        UpdateSavedViewRequest request = new()
        {
            Name = "Updated view",
            IsShared = true,
            SortJson = """["-name"]""",
        };

        request.Name.ShouldBe("Updated view");
        request.IsShared.ShouldBeTrue();
        request.SortJson.ShouldNotBeNull();
    }
}
