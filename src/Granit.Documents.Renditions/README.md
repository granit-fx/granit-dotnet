# Granit.Documents.Renditions

Provider-driven rendition (thumbnail / web / print / poster) generation for
`Granit.Documents`. This package contains contracts only — provider
implementations live in separate packages.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Renditions
```

Plus at least one provider and the storage companion:

```bash
dotnet add package Granit.Documents.Renditions.EntityFrameworkCore
dotnet add package Granit.Documents.Renditions.Imaging   # image -> image
dotnet add package Granit.Documents.Renditions.Pdf       # pdf -> image (via Granit.Browsing)
dotnet add package Granit.Documents.Renditions.Office    # docx/xlsx/pptx -> pdf (via LibreOffice)
```

## Surface

- `DocumentRendition` aggregate keyed by `(DocumentVersionId, Type, Format)`.
- `IRenditionProvider` — single-step transformation from one MIME type to another.
- `IRenditionPipeline` — BFS solver that chains providers (capped at
  `GranitRenditionsOptions.MaxChainLength`, default 3 hops).
- `IRenditionStore` — persistence abstraction.
- `RenditionType` — `Thumbnail`, `Web`, `Print`, `Poster`.
- `RenditionStatus` lifecycle — `Pending` → `Generating` → `Ready` / `Failed`.
- Domain events — `RenditionGeneratedEvent`, `RenditionFailedEvent`.

## Pipeline chaining

Office documents reach an image target through a two-hop chain:

```text
docx  --(Office provider)-->  application/pdf  --(Pdf provider)-->  image/png  --(Imaging provider)-->  image/webp
```

The solver picks the shortest path automatically; ties break on registration order.
