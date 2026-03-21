# Granit.Bundle.OpenIddict

Meta-package for the complete Granit OpenIddict module family.

## Usage

Add a single NuGet reference to get all 9 OpenIddict packages:

```xml
<PackageReference Include="Granit.Bundle.OpenIddict" />
```

Then add the bundle module:

```csharp
[DependsOn(typeof(GranitOpenIddictBundleModule))]
public sealed class MyAppModule : GranitModule { }
```

## Included packages

- `Granit.OpenIddict` — abstractions, interfaces, options
- `Granit.OpenIddict.EntityFrameworkCore` — entities, DbContext
- `Granit.OpenIddict.Identity` — IIdentityProvider bridge
- `Granit.OpenIddict.Endpoints` — account self-service API
- `Granit.OpenIddict.Admin.Endpoints` — admin API
- `Granit.OpenIddict.Client` — external login providers
- `Granit.OpenIddict.Seeding` — declarative seeding
- `Granit.OpenIddict.BackgroundJobs` — token cleanup
- `Granit.OpenIddict.Passkeys` — WebAuthn/FIDO2
