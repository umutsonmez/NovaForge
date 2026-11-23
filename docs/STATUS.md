# Proje Durumu ve Yol Haritası

_Son güncelleme: 2026-11-23_

## Ortam bulguları
- Unity Editor / Unity Hub **kurulu değil** → Unity'ye bağlı hiçbir şey derlenemedi/çalıştırılamadı.
- .NET SDK 8 ve 10 kurulu → motor-bağımsız Core, Unity'nin derleyici kısıtlarıyla (netstandard2.1, C# 9) derlendi ve test edildi.

## Faz 1 — Motor-bağımsız Core ✅ (doğrulandı)
Derleme: 0 uyarı / 0 hata. Testler: **103/103 geçti** (`dotnet test`).
Foundation, Config + Remote Config, Save, Economy, Rewards, Entitlements, Ads (kurallar), IAP (akış + sunucu validator istemcisi),
Analytics, Consent, Progression, Level Session, Statistics, Missions, Achievements, Daily Rewards, Tutorial, Difficulty, Shop,
Localization, Object Pool, Composition root (`NovaForgeCore`).

## Faz 1 — Unity köprüsü ⚠️ (yazıldı, doğrulanmadı)
`NovaForgeApp`, `UnityLogSink`, `UnityStorage`, `UnityHttpTransport`, `SafeAreaFitter`.
Yalnızca uzun süredir kararlı API'ler kullanıldı ama **Unity'de ilk import'ta derlenmesi doğrulanmalı.**

## Sıradaki fazlar (yapılmadı)
| Faz | İçerik | Ön koşul |
|---|---|---|
| 2 | Unity projesi kurulumu, Core'un Unity Test Runner'da çalıştırılması, Unity köprüsünün doğrulanması | Unity 6.3 LTS kurulumu |
| 3 | UI framework (ScreenManager, PopupManager, Navigation, ekranlar, tema), Audio (pooling, mixer), Input (tap/swipe/drag/long-press), Scene transitions, Addressables | Faz 2 |
| 4 | SDK adaptörleri: Unity IAP 5.x, reklam ağı (AdMob veya LevelPlay — karar gerekli), Firebase Analytics/Remote Config/Crashlytics, Google UMP + iOS ATT | Faz 2 + hesaplar |
| 5 | Editor araçları: NOVAFORGE menüsü, Create New Game, validators (config/monetization/localization), Level Editor, build config | Faz 3 |
| 6 | Oyun şablonları (Puzzle, Arcade, Hypercasual, Idle, Simulation) | Faz 3-5 |
| 7 | Referans oyun (tilt + renk karıştırma bulmacası), 30+ bölüm, solver ile çözülebilirlik doğrulaması | Faz 3-5 |
| 8 | Satın alma doğrulama backend'i, sandbox testleri, BUILD_AND_RELEASE süreci | Faz 4 |

Henüz yazılmayan dokümanlar (ilgili sistemle birlikte gelecek; boş placeholder üretilmedi):
`GAME_CREATION_GUIDE.md`, `LEVEL_EDITOR_GUIDE.md`, `BUILD_AND_RELEASE.md`.

## Bilinen sınırlar
- Save checksum'ı bozulmayı tespit eder, hileye karşı koruma değildir.
- Günlük ödül ve reklam günlük limitleri cihaz saatine dayanır (geri alma tespiti var); değerli ödüller için sunucu saati önerilir.
- `AdsService` ödüllü reklam günlük sayacı bellekte tutulur (uygulama yeniden başlayınca sıfırlanır); kalıcı sınır için `EconomyDefinition.AddDailyEarnLimit` kullanılmalı.
- `ICloudSaveProvider` yalnızca arayüz.
- Satın alma doğrulama sunucusu yok; release build'de `DevelopmentAcceptAllValidator` engellenir.
