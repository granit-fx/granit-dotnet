# Granit.Encryption.EntityFrameworkCore

EF Core field-level encryption for Granit applications. Provides the `[Encrypted]`
attribute and `EncryptedStringConverter` backed by `IStringEncryptionService` for
transparent encrypt/decrypt on read and write.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Encryption.EntityFrameworkCore
```

## Dependencies

- `Granit`
- `Granit.Encryption`
- `Granit.Persistence`

## Integration

The attribute and converter only encrypt once they are wired into your `DbContext`.

1. Inject `IStringEncryptionService?` into the context constructor — nullable so the
   context still resolves when encryption is not configured:

   ```csharp
   public sealed class MyDbContext(
       DbContextOptions<MyDbContext> options,
       IStringEncryptionService? encryptionService = null) : GranitDbContext(options)
   {
       private readonly IStringEncryptionService? _encryptionService = encryptionService;
   }
   ```

2. Apply the conventions in `OnGranitModelCreating` (or `OnModelCreating`) only when
   the service is present:

   ```csharp
   if (_encryptionService is not null)
       modelBuilder.ApplyEncryptionConventions(_encryptionService);
   ```

3. Mark string properties with `[Encrypted]`:

   ```csharp
   [Encrypted]
   public string Diagnosis { get; private set; }
   ```

### Per-entity key isolation (crypto-shredding)

`[Encrypted(KeyIsolation = true)]` routes the property through the encryption
interceptors instead of the value converter, using a per-entity key from
`IEntityEncryptionKeyStore`. This requires two extra steps:

- Register an `IEntityEncryptionKeyStore` implementation (a Vault provider or your own).
- Opt in to the interceptors in your `DbContext` options, **after** `UseGranitInterceptors`:

  ```csharp
  options.UseGranitInterceptors(sp);
  options.UseGranitEncryptionInterceptors(sp);
  ```

Without the key store registered the interceptors silently skip, so isolated fields are
left unencrypted.

## Documentation

See the [full documentation](https://granit-fx.dev).
