; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
GRMIGA001 | Migrations | Error | DropColumnWithoutContractAnalyzer, IsEnabledByDefault=True
GRMIGA002 | Migrations | Error | RenameColumnForbiddenAnalyzer, IsEnabledByDefault=True
GRMIGA003 | Migrations | Warning | NullableColumnInExpandAnalyzer, IsEnabledByDefault=True
GRMIGA004 | Migrations | Warning | AlterColumnWithoutContractAnalyzer, IsEnabledByDefault=True
GRSEC001 | Security | Warning | DateTimeNowAnalyzer, IsEnabledByDefault=True
GRSEC002 | Security | Warning | GuidNewGuidAnalyzer, IsEnabledByDefault=True
GRSEC003 | Security | Error | HardcodedSecretAnalyzer, IsEnabledByDefault=True
GRSEC004 | Security | Warning | DirectCookieAccessAnalyzer, IsEnabledByDefault=True
GRSEC005 | Security | Warning | PrivacyExportSubjectSubstitutionAnalyzer, IsEnabledByDefault=True
GREF001 | EntityFramework | Warning | SynchronousSaveChangesAnalyzer, IsEnabledByDefault=True
GRAPI001 | Api | Warning | UntypedResultsAnalyzer, IsEnabledByDefault=True
GRAPI002 | Api | Warning | TypedResultsBadRequestAnalyzer, IsEnabledByDefault=True
GRAPI003 | Api | Warning | MinimalApiServiceParameterAnalyzer, IsEnabledByDefault=True
GRMOD001 | Architecture | Error | CrossModuleReferenceAnalyzer, IsEnabledByDefault=True
GRSEC010 | Security | Error | TagListPiiAnalyzer, IsEnabledByDefault=True
GRSEC011 | Security | Error | LoggerMessagePiiAnalyzer, IsEnabledByDefault=True
GRBROWSING001 | Security | Warning | EvaluateAsyncStringInterpolationAnalyzer, IsEnabledByDefault=True (Granit.Browsing IBrowserPage JS-injection guard)
