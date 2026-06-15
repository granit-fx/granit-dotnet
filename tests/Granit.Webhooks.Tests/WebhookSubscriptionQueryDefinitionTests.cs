using Granit.QueryEngine;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Queries;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

// Issue #2767: TargetUrl is a HttpsUrl value object (SingleValueObject<string>) mapped via a
// ValueConverter. It supports equality/IN filtering (whole-value round-trips), but not substring
// search/LIKE — so it is .Filterable() yet absent from GlobalSearch.
public sealed class WebhookSubscriptionQueryDefinitionTests
{
    [Fact]
    public void TargetUrl_value_object_column_is_filterable()
    {
        WebhookSubscriptionQueryDefinition definition = new();

        ColumnDescriptor targetUrl = definition.GetColumns()
            .Single(c => c.PropertyName == nameof(WebhookSubscription.TargetUrl));

        targetUrl.IsFilterable.ShouldBeTrue();
    }

    [Fact]
    public void GlobalSearch_targets_the_plain_string_EventType()
    {
        WebhookSubscriptionQueryDefinition definition = new();

        definition.GetGlobalSearchProperties().ShouldContain(nameof(WebhookSubscription.EventType));
    }
}
