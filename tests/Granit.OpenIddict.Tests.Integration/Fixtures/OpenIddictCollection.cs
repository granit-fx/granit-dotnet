using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Fixtures;

/// <summary>
/// xUnit collection definition that shares a single <see cref="OpenIddictTestApplication"/>
/// (and its PostgreSQL container) across all test classes.
/// </summary>
[CollectionDefinition("openiddict-integration")]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xUnit collection fixture naming convention")]
public sealed class OpenIddictIntegrationFixtureCollection : ICollectionFixture<OpenIddictTestApplication>;
