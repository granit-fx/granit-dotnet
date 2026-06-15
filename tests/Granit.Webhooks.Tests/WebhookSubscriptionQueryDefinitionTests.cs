using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Queries;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

// Issue #2767: TargetUrl is a HttpsUrl value object (SingleValueObject<string>) mapped via a
// ValueConverter, so it cannot be filtered (an Eq filter mistranslates; substring cannot reach
// LIKE). It must stay display-only — not .Filterable() — so the grid does not advertise a broken
// filter affordance.
public sealed class WebhookSubscriptionQueryDefinitionTests
{
    [Fact]
    public void TargetUrl_value_object_column_is_not_filterable()
    {
        WebhookSubscriptionQueryDefinition definition = new();

        ColumnDescriptor targetUrl = definition.GetColumns()
            .Single(c => c.PropertyName == nameof(WebhookSubscription.TargetUrl));

        targetUrl.IsFilterable.ShouldBeFalse();
    }

    [Fact]
    public void GlobalSearch_targets_the_plain_string_EventType()
    {
        WebhookSubscriptionQueryDefinition definition = new();

        definition.GetGlobalSearchProperties().ShouldContain(nameof(WebhookSubscription.EventType));
    }
}
