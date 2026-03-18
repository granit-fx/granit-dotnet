---
title: "Factory Pattern \u2014 React Provider Creation"
description: How the Factory pattern drives every @granit/* package — encapsulating complex initialization (Keycloak, i18next, Axios) behind simple TypeScript factory functions with minimal config.
sidebar:
  label: Factory
  order: 1
---

## Definition

The Factory pattern encapsulates complex object creation logic behind simple
functions. Callers provide minimal configuration and receive ready-to-use
instances without knowing initialization details.

This is the dominant pattern in granit-front — every package exposes at least
one factory.

## Diagram

```mermaid
graph LR
    Config["Config object"] --> Factory["createXxx()"]
    Factory --> Instance["Ready-to-use instance"]
```

## Implementation in Granit

| Factory | Package | Creates |
|---------|---------|---------|
| `createLogger(prefix, options?)` | `@granit/logger` | Logger with configured transports |
| `createApiClient(config)` | `@granit/api-client` | Axios instance with Bearer interceptor |
| `createAuthContext<T>()` | `@granit/react-authentication` | Generic, type-safe auth context + hook |
| `createLocalization(config?)` | `@granit/localization` | Isolated i18next instance |
| `createReactLocalization(config?)` | `@granit/react-localization` | i18next with `initReactI18next` plugin |
| `createSignalRTransport(config)` | `@granit/notifications-signalr` | SignalR notification transport |
| `createSseTransport(config)` | `@granit/notifications-sse` | SSE notification transport |
| `createKlaroCookieConsentProvider(options)` | `@granit/cookies-klaro` | Klaro CMP adapter |
| `createStorage<T>(key, options?)` | `@granit/storage` | Typed localStorage/sessionStorage accessor |
| `createMockProvider<T>()` | `@granit/react-authentication` | Test provider using same context |

## Rationale

Factory functions keep the public API surface minimal while hiding initialization
complexity (transport wiring, plugin injection, default configuration). They also
enable tree-shaking — unused factories are eliminated at build time.

## Usage example

```ts
import { createLogger } from '@granit/logger';
import { createApiClient } from '@granit/api-client';
import { createAuthContext } from '@granit/react-authentication';
import type { BaseAuthContextType } from '@granit/authentication';

// Logger with console transport
const logger = createLogger('app');

// HTTP client with automatic Bearer injection
const api = createApiClient({ baseURL: import.meta.env.VITE_API_URL });

// Typed auth context for the application
interface AuthContextType extends BaseAuthContextType {
  register: () => void;
}
export const { AuthContext, useAuth } = createAuthContext<AuthContextType>();
```

## Further reading

- [Factory Method — refactoring.guru](https://refactoring.guru/design-patterns/factory-method)
