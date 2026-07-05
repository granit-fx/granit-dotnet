using Granit.DataExchange.Export;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Exports;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests.Definitions;

public sealed class WebhookSubscriptionExportDefinitionTests
{
    private static readonly WebhookSubscriptionExportDefinition Sut = new();

    [Fact]
    public void Name_is_stable() =>
        Sut.Name.ShouldBe("Granit.Webhooks.WebhookSubscriptionExport");

    [Fact]
    public void HasComplexFields_is_true() =>
        ((IExportDefinitionDescriptor)Sut).HasComplexFields.ShouldBeTrue();

    [Fact]
    public void OnIncompatibleField_default_is_Throw() =>
        ((IExportDefinitionDescriptor)Sut).OnIncompatibleField.ShouldBe(OnIncompatibleFieldPolicy.Throw);

    [Fact]
    public void SigningKeys_field_has_RequiresHierarchy()
    {
        ExportFieldDescriptor field = Sut.GetFields()
            .Single(f => f.PropertyPath == "SigningKeys");

        field.RequiresHierarchy.ShouldBeTrue();
        field.ValueSelector.ShouldNotBeNull();
        field.SelectorType.ShouldBe(typeof(List<WebhookSigningKeySnapshot>));
    }

    [Fact]
    public void SigningKeys_selector_maps_lifecycle_metadata_only()
    {
        ExportFieldDescriptor field = Sut.GetFields()
            .Single(f => f.PropertyPath == "SigningKeys");

        WebhookSubscription subscription = BuildSubscriptionWithRotation();
        var snapshots = (List<WebhookSigningKeySnapshot>)field.ValueSelector!(subscription)!;

        snapshots.Count.ShouldBe(2);

        WebhookSigningKeySnapshot active = snapshots.Single(s => s.Status == WebhookSigningKeyStatus.Active);
        active.Id.ShouldNotBe(Guid.Empty);
        active.ExpiresAt.ShouldBeNull();
        active.RevokedAt.ShouldBeNull();

        WebhookSigningKeySnapshot retired = snapshots.Single(s => s.Status == WebhookSigningKeyStatus.Retired);
        retired.ExpiresAt.ShouldNotBeNull();
        retired.RevokedAt.ShouldBeNull();
    }

    [Fact]
    public void SigningSecretHint_is_exported_as_scalar()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = Sut.GetFields();
        ExportFieldDescriptor? hint = fields.FirstOrDefault(f => f.PropertyPath == "SigningSecretHint");

        hint.ShouldNotBeNull();
        hint!.RequiresHierarchy.ShouldBeFalse();
    }

    [Fact]
    public void ProtectedSecret_is_not_exported()
    {
        IReadOnlyList<ExportFieldDescriptor> fields = Sut.GetFields();

        fields.ShouldNotContain(f =>
            f.PropertyPath.Contains("ProtectedSecret", StringComparison.OrdinalIgnoreCase) ||
            (f.PropertyPath.Contains("Secret", StringComparison.OrdinalIgnoreCase) && !f.PropertyPath.Contains("Hint")));
    }

    // ---- Helpers -----------------------------------------------------------

    private static WebhookSubscription BuildSubscriptionWithRotation()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var sub = WebhookSubscription.Create(
            id: Guid.NewGuid(),
            targetUrl: "https://example.com/hook",
            eventType: "test.event",
            signingKeyId: Guid.NewGuid(),
            protectedSecret: "protected-initial",
            createdAt: now);

        // Reflect to call internal RotateSigningKey so the test doesn't depend on the writer
        typeof(WebhookSubscription)
            .GetMethod("RotateSigningKey",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(sub, [Guid.NewGuid(), "protected-new", now.AddHours(1), TimeSpan.FromHours(24), null]);

        return sub;
    }
}
