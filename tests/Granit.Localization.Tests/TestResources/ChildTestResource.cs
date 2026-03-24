using Granit.Localization;

namespace Granit.Localization.Tests.TestResources;

[LocalizationResourceName("Child")]
[InheritResource(typeof(ParentTestResource))]
public sealed class ChildTestResource;
