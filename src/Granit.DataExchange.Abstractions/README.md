# Granit.DataExchange.Abstractions

Inter-module contracts for Granit.DataExchange: declarative `ExportDefinition<T>`
base class, fluent builders (`ExportDefinitionBuilder<T>`, `ExportFieldBuilder<T>`),
field descriptors, auto-source hooks and extra-property providers. Reference this
package from any module that declares exports; reference `Granit.DataExchange` only
from hosts that execute them.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.DataExchange.Abstractions
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
