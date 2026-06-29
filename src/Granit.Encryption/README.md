# Granit.Encryption

String encryption/decryption with AES-256-CBC and Vault Transit providers for Granit applications.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Encryption
```

## Dependencies

- `Granit`

## Configuration

A pass phrase is **mandatory**. Options are bound from the `Encryption`
configuration section with `ValidateOnStart`, and `AllowEphemeralPassPhrase`
defaults to `false`, so a missing `Encryption:PassPhrase` fails the host at
startup:

```json
{
  "Encryption": {
    "PassPhrase": "<32+ character secret — inject from Granit.Vault / env>",
    "ProviderName": "Aes",
    "KeySize": 256,
    "AllowEphemeralPassPhrase": false
  }
}
```

`ProviderName` is `Aes` (AES-256-CBC) or `Vault` (Transit; set `VaultKeyName`).
`AllowEphemeralPassPhrase: true` is for dev/test only.

## Usage

The module's `ConfigureServices` calls `AddGranitEncryption()` for you — wire
the module into your dependency graph rather than calling it by hand:

```csharp
[DependsOn(typeof(GranitEncryptionModule))]   // or GranitEncryptionEntityFrameworkCoreModule, which pulls this in transitively
public sealed class AppHostModule : GranitModule { }
```

Then inject the services:

```csharp
public sealed class MyService(IStringEncryptionService cipher, ICryptoShredder shredder)
{
    public string Protect(string plain) => cipher.Encrypt(plain);
    public string Reveal(string cipherText) => cipher.Decrypt(cipherText);
    // shredder.ShredAsync(key) drops the per-subject key for crypto-shredding (GDPR erasure).
}
```

For EF Core field-level encryption, the `Granit.Encryption.EntityFrameworkCore`
package adds the `[Encrypted]` attribute / `EncryptedStringConverter`.

## Documentation

See the [full documentation](https://granit-fx.dev).
