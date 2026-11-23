# NOVAFORGE

Ticari mobil oyunlar (Android + iOS, Unity) için yeniden kullanılabilir üretim altyapısı.
Çekirdek paket: **`com.novaforge.core`** (`Packages/com.novaforge.core`).

> **Durum:** Core + Unity köprüsü + AdMob adaptörü Unity 6.3 LTS'de (6000.3.29f1) derleniyor; 103 test hem Unity'de hem dotnet'te geçiyor.
> Core, UI, ses ve referans oyun Tiltwell (36 bölüm) editörde oynanabilir. IAP adaptörü, Level Editor ve cihaz testleri henüz yapılmadı. Ayrıntı: [docs/STATUS.md](docs/STATUS.md).

## Hızlı bakış

| Katman | Assembly | Unity bağımlılığı | Doğrulama |
|---|---|---|---|
| İş mantığı (economy, save, ads/IAP kuralları, analytics, progression...) | `NovaForge.Core` | Yok (`noEngineReferences: true`) | 103 test: Unity Test Runner + `dotnet test` |
| Platform köprüsü (bootstrap, log, HTTP, safe area) | `NovaForge.Unity` | Var | Derleniyor; PlayMode testi yok |
| AdMob adaptörü | `NovaForge.Ads.AdMob` | Var + `com.google.ads.mobile` >= 11 | Derleniyor (11.7.0); cihaz testi yok |
| UI framework (navigasyon, view'lar, ayarlar, editor menüsü) | `NovaForge.UI`, `NovaForge.Editor` | uGUI 2.0 + TMP | 5 PlayMode testi; bkz. [docs/UI_FRAMEWORK.md](docs/UI_FRAMEWORK.md) |
| Referans oyun Tiltwell (eğ + renk karıştır bulmaca) | `com.novaforge.tiltwell` | Core + UI | 22 dotnet + 2 uçtan uca PlayMode testi; bkz. [docs/TILTWELL.md](docs/TILTWELL.md) |
| Testler | `NovaForge.Core.Tests` | Unity Test Framework 1.6 / NUnit 3 | Aynı kaynak hem Unity'de hem dotnet'te çalışır |

## Gereksinimler

- Unity **6.3 LTS** (6000.3) önerilir; paket minimum `6000.0`.
- `com.unity.nuget.newtonsoft-json` 3.2.x (paket bağımlılığı, otomatik gelir).
- Unity olmadan doğrulama için: .NET SDK 8+.

## Unity içinde test (Sandbox projesi)

`Projects/Sandbox`, paketi `file:` referansıyla kullanan geliştirme projesidir (AdMob dahil).
```powershell
& "C:/Program Files/Unity/Hub/Editor/6000.3.29f1/Editor/Unity.exe" -batchmode -nographics `
  -projectPath Projects/Sandbox -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -
```

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
- [TILTWELL.md](docs/TILTWELL.md) — referans oyun: kurallar, bölüm üretimi, sahne builder
- [STATUS.md](docs/STATUS.md) — ne bitti, ne kaldı, bilinen sınırlar
- [NAME_RESEARCH.md](docs/NAME_RESEARCH.md) — referans oyun isim araştırması
