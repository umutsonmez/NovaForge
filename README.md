# NOVAFORGE

Ticari mobil oyunlar (Android + iOS, Unity) için yeniden kullanılabilir üretim altyapısı.
Çekirdek paket: **`com.novaforge.core`** (`Packages/com.novaforge.core`).

> **Durum:** Faz 1 (motor-bağımsız Core) tamamlandı ve test edildi. Unity katmanı, UI, Editor araçları,
> SDK adaptörleri ve referans oyun henüz yapılmadı. Ayrıntı: [docs/STATUS.md](docs/STATUS.md).

## Hızlı bakış

| Katman | Assembly | Unity bağımlılığı | Doğrulama |
|---|---|---|---|
| İş mantığı (economy, save, ads/IAP kuralları, analytics, progression...) | `NovaForge.Core` | Yok (`noEngineReferences: true`) | `dotnet test` ile 103 test geçiyor |
| Platform köprüsü (bootstrap, log, HTTP, safe area) | `NovaForge.Unity` | Var | **Henüz Unity'de derlenmedi** |
| Testler | `NovaForge.Core.Tests` | Unity Test Framework / NUnit 3 | Aynı kaynak hem Unity'de hem dotnet'te çalışır |

## Gereksinimler

- Unity **6.3 LTS** (6000.3) önerilir; paket minimum `6000.0`.
- `com.unity.nuget.newtonsoft-json` 3.2.x (paket bağımlılığı, otomatik gelir).
- Unity olmadan doğrulama için: .NET SDK 8+.

## Unity'siz build ve test

```bash
cd tools/dotnet
dotnet test NovaForge.slnx
```

`tools/dotnet` projeleri `Runtime/Core` ve `Tests/Editor` altındaki **aynı** `.cs` dosyalarını derler
(netstandard2.1 + C# 9, Unity ile aynı kısıtlar; uyarılar hata sayılır).

## Bir oyuna ekleme

`Packages/manifest.json`:
```json
"com.novaforge.core": "file:../../NOVAFORGE/Packages/com.novaforge.core"
```
veya git URL (`...git?path=/Packages/com.novaforge.core#v0.1.0`). Ardından `NovaForgeApp`'ten türeyen bir
bootstrap sınıfı yazıp `GameSetup` doldurulur — bkz. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Dokümanlar

- [ARCHITECTURE.md](docs/ARCHITECTURE.md) — katmanlar, bağımlılık kuralları, composition root
- [CORE_MODULES.md](docs/CORE_MODULES.md) — modül modül API ve garantiler
- [MONETIZATION_GUIDE.md](docs/MONETIZATION_GUIDE.md), [ADS_INTEGRATION.md](docs/ADS_INTEGRATION.md), [IAP_INTEGRATION.md](docs/IAP_INTEGRATION.md)
- [CODING_STANDARDS.md](docs/CODING_STANDARDS.md), [TESTING_GUIDE.md](docs/TESTING_GUIDE.md), [VERSIONING.md](docs/VERSIONING.md)
- [STATUS.md](docs/STATUS.md) — ne bitti, ne kaldı, bilinen sınırlar
- [NAME_RESEARCH.md](docs/NAME_RESEARCH.md) — referans oyun isim araştırması
