# Core Modülleri

Tümü `Packages/com.novaforge.core/Runtime/Core` altında. Testler `Tests/Editor`.

## Foundation
| Tip | Görev | Garanti |
|---|---|---|
| `ServiceRegistry` | DI container | Lazy singleton, alias, döngüsel bağımlılık tespiti, ters sırada dispose |
| `EventBus` | Tipli pub/sub | Dispatch sırasında abonelik değişimi güvenli; handler hatası izole |
| `LoggingService`, `ILogSink`, `RingBufferLogSink` | Loglama | Bozuk sink diğerlerini etkilemez; ring buffer = crash breadcrumb |
| `ErrorHandler` | Merkezi hata raporu | Asla throw etmez; re-entrancy korumalı; dış sink (Crashlytics vb.) desteği |
| `StateMachine<T>` | FSM | Beyaz liste geçişler, Enter/Exit içinden istenen geçişler kuyruklanır |
| `GameLifecycle` | Pause/Resume/Quit → event | Standart `GameFlowState` geçiş tablosu |
| `ObjectPool<T>` | GC'siz yeniden kullanım | Çift release tespiti, max boyut |
| `IClock` / `ManualClock` | Zaman soyutlaması | Tüm zaman kuralları test edilebilir |

## Config
- `ConfigSchema`: her ayarlanabilir değer tip + varsayılan + aralık ile **beyan edilir**. Beyan edilmeyen uzak anahtarlar yok sayılır.
- `ConfigurationManager`: katmanlar: şema varsayılanı < local < remote cache < remote. Geçersiz değer anahtar bazında reddedilir, önceki geçerli değer kalır.
- `RemoteConfigService`: timeout, hata toleransı, yalnızca **geçerli** değerleri offline cache'e yazar.
- `CoreConfig`: reklam sıklığı, zorluk, günlük ödül takvimi vb. Core anahtarları. Interstitial aralığının 30 sn altına indirilmesi şema seviyesinde engellenir.

## Data
- `SaveManager` + `SaveSlot<T>`: versiyonlu zarf, SHA-256 bütünlük, migration zinciri (`ISaveMigration`), `ISaveValidatable`, backup'tan kurtarma.
- `FileSaveStorage`: atomik yazma + `.bak`; anahtar doğrulama (path traversal yok).
- `ICloudSaveProvider`, `ICloudConflictResolver`: **yalnızca soyutlama**; somut sağlayıcı yok.
- Not: checksum bozulmayı tespit eder, **hile koruması değildir**. Gerçek para hakları mağaza/sunucu ile yeniden doğrulanır.

## Economy
- `EconomyDefinition`: oyun kendi kaynaklarını beyan eder (coin, gem, energy, ticket, star, item...). Hiçbiri zorunlu değil.
- `EconomyService` (`IWallet`): çok kaynaklı **atomik** işlem; negatif bakiye, max aşımı, overflow, bilinmeyen kaynak, **tekrarlanan işlem id'si** ve **günlük kazanç limiti** (kaynak+kaynak türü bazında) engellenir. Kalıcılaştırma başarısızsa işlem geri alınır.
- `RewardService`: `grantId` başına tek seferlik ödül.
- `EntitlementService`: kalıcı haklar (`no_ads`, premium, abonelik süresi). Kalıcı hak asla süreliye düşürülmez.
- Sınır: hatırlanan işlem id'si sayısı 5000 (FIFO). İlk-bitirme ödülleri ayrıca progression durumuyla korunur; IAP kendi kalıcı ledger'ını kullanır.

## Ads — bkz. [ADS_INTEGRATION.md](ADS_INTEGRATION.md)
## Purchasing — bkz. [IAP_INTEGRATION.md](IAP_INTEGRATION.md)

## Analytics & Privacy
- `ConsentService`: analytics izni, kişiselleştirilmiş reklam izni, ATT durumu, çocuk kullanıcı, kabul edilen politika versiyonu. **Bilinmiyor = izin yok.**
- `AnalyticsService`: izin yoksa olay **gönderilmez ve kuyruklanmaz**; isim/parametre Firebase kurallarına göre temizlenir; birden çok sağlayıcıya dağıtır. PII için API yok.
- `SessionTracker`: `session_started` (session_number, days_since_install), `session_ended` (duration). DAU/MAU/D1/D7/D30, ARPDAU, dönüşüm **analytics backend'inde** bu olaylardan hesaplanır.
- `AnalyticsEvents`: talimattaki tüm standart olay adları.

## Progression
- `ILevelCatalog` / `ListLevelCatalog`, `ProgressionService` (kilit açma, 0-3 yıldız, en iyi skor, deneme/başarısızlık).
- `LevelSessionService`: tek bir deneme orkestrasyonu: progression + istatistik + ödül (idempotent) + analytics + reklam temposu.
- `ScoreTracker`, `ObjectiveTracker`, `DifficultyService` (art arda başarısızlıkta sınırlar içinde kolaylaştırma).
- `PlayerStatistics`: monoton sayaçlar → görev ve başarılar.

## Meta
- `DailyRewardService`: config'ten takvim, günde bir, kaçırılan günde seri sıfırlama (opsiyonel), **saat geri alma tespiti**.
- `MissionService`: günlük/haftalık/kalıcı, istatistik farkı ile ilerleme, oyuncu talep eder.
- `AchievementService`: eşik aşılınca otomatik açılır, ödül bir kez.
- `TutorialService`: adım dizileri, eski UI callback'lerine karşı korumalı, yalnızca tamamlanma kalıcı.

## Shop
- `ShopItem`: ya soft currency fiyatlı ya da mağaza ürünü (fiyat **mağazadan**, kodda değil).
- `ShopService`: soft satın alma = tek atomik işlem; satın alma limiti kalıcı istatistikle; sahip olunan non-consumable gizlenir.

## Localization
- 8 dil (en, tr, de, fr, es, pt, ja, ko), kültür çözümleme (`pt-BR → pt`).
- Geri düşme: seçili dil → İngilizce → görünür `#key#`. Eksik anahtarlar birer kez loglanır.
- Doğrulama: `FindMissingKeys`, `FindPlaceholderMismatches` (Editor validator bunları kullanacak).

## Composition
- `GameSetup` + `NovaForgeCore` — bkz. ARCHITECTURE.md.
