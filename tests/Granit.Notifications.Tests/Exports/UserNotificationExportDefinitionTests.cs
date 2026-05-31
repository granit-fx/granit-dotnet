using System.Text.Json;
using Granit.DataExchange.Export;
using Granit.Notifications.Domain;
using Granit.Notifications.Exports;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests.Exports;

public sealed class UserNotificationExportDefinitionTests
{
    private static readonly UserNotificationExportDefinition Sut = new();
    private static IReadOnlyList<ExportFieldDescriptor> Fields =>
        ((IExportDefinitionDescriptor)Sut).GetFields();

    [Fact]
    public void Name_is_stable() =>
        Sut.Name.ShouldBe("Granit.Notifications.UserNotificationExport");

    [Fact]
    public void HasComplexFields_is_true() =>
        ((IExportDefinitionDescriptor)Sut).HasComplexFields.ShouldBeTrue();

    [Fact]
    public void OnIncompatibleField_default_is_Throw() =>
        ((IExportDefinitionDescriptor)Sut).OnIncompatibleField.ShouldBe(OnIncompatibleFieldPolicy.Throw);

    [Fact]
    public void Data_field_has_RequiresHierarchy()
    {
        ExportFieldDescriptor field = Fields.Single(f => f.PropertyPath == "Data");
        field.RequiresHierarchy.ShouldBeTrue();
        field.ValueSelector.ShouldNotBeNull();
        field.SelectorType.ShouldBe(typeof(JsonElement));
    }

    [Fact]
    public void Data_selector_returns_JsonElement_from_notification()
    {
        ExportFieldDescriptor field = Fields.Single(f => f.PropertyPath == "Data");
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>("""{"subject":"Hello","body":"World"}""");
        var notification = UserNotification.Create(
            id: Guid.NewGuid(),
            notificationId: Guid.NewGuid(),
            notificationTypeName: "test.notification",
            severity: NotificationSeverity.Info,
            recipientUserId: "user-1",
            data: payload,
            createdAt: DateTimeOffset.UtcNow);

        field.ValueSelector!(notification).ShouldBeOfType<JsonElement>()
            .GetProperty("subject").GetString().ShouldBe("Hello");
    }

    [Fact]
    public void Scalar_fields_do_not_include_Data()
    {
        IReadOnlyList<ExportFieldDescriptor> scalarFields = Fields.Where(f => !f.RequiresHierarchy).ToList();
        scalarFields.ShouldNotContain(f => f.PropertyPath == "Data");
    }
}
