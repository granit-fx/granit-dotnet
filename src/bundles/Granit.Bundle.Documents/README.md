# Granit.Bundle.Documents

Meta-package grouping Granit templating and document generation modules
for HTML-to-PDF rendering, Excel generation, and Scriban template management.

Part of the [granit](https://granit-fx.dev) framework.

## Included packages

| Package | Role |
| --- | --- |
| `Granit.Templating` | Template engine abstractions, pipeline |
| `Granit.Templating.Scriban` | Scriban template engine |
| `Granit.Templating.EntityFrameworkCore` | Template EF Core store + cache |
| `Granit.DocumentGeneration` | `IDocumentGenerator`, `IDocumentRenderer` |
| `Granit.DocumentGeneration.Pdf` | HTML-to-PDF via PuppeteerSharp |
| `Granit.DocumentGeneration.Excel` | Excel generation via ClosedXML |

## Installation

```bash
dotnet add package Granit.Bundle.Documents
```

## Documentation

See the [templating documentation](https://granit-fx.dev).
