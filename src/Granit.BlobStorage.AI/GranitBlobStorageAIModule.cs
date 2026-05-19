using Granit.AI;
using Granit.Modularity;

namespace Granit.BlobStorage.AI;

/// <summary>
/// Granit module for AI-powered blob classification and validation.
/// </summary>
/// <remarks>
/// <para>
/// Adds an <see cref="IBlobValidator"/> (Order = 100) that uses an LLM to classify uploaded files
/// by category (invoice, identity document, photo, contract, etc.) and detect PII in filenames.
/// Runs after the cheaper built-in validators (magic bytes, max size).
/// </para>
/// <para>
/// Also exposes <see cref="IAIBlobClassifier"/> for consumers that need classification
/// outside the validation pipeline.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitAIModule), typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageAIModule : GranitModule;
