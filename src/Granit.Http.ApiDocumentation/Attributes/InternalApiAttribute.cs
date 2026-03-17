namespace Granit.Http.ApiDocumentation.Attributes;

/// <summary>
/// Marks a controller or action as internal. Endpoints decorated with this attribute
/// are silently excluded from all generated OpenAPI documents.
/// Use for inter-service webhooks, synchronization endpoints, or raw admin routes
/// that must not appear in public-facing documentation.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class InternalApiAttribute : Attribute;
