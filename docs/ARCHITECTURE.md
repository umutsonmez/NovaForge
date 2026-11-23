# Mimari

## Hedef
Tür-bağımsız (puzzle, arcade, hypercasual, idle, simulation) ortak sistemler tek pakette; her oyun yalnızca kendi
mekaniğini, bölümlerini ve görsel kimliğini yazar.

## Katmanlar

```
┌──────────────────────────────────────────────────────────┐
│ Oyun (örn. referans oyun)                                │  mekanik, level data, UI ekranları, tema
│   GameBootstrap : NovaForgeApp  → GameSetup doldurur     │
├──────────────────────────────────────────────────────────┤
│ NovaForge.Unity  (UnityEngine'e bağlı, ince)             │  MonoBehaviour köprüleri, UnityWebRequest,
│   NovaForgeApp, UnityLogSink, UnityHttpTransport, ...    │  SafeArea, ileride: UI framework, audio, input,
│   [ileride] SDK adaptörleri (ayrı asmdef'ler)            │  AdMob/LevelPlay/Unity IAP/Firebase adaptörleri
├──────────────────────────────────────────────────────────┤
│ NovaForge.Core  (noEngineReferences: true)               │  TÜM iş kuralları: economy, save, ads/IAP akışları,
│   Foundation · Config · Data · Economy · Ads · Purchasing│  analytics, consent, progression, meta, shop,
│   Analytics · Privacy · Progression · Meta · Shop · Loc  │  localization
└──────────────────────────────────────────────────────────┘
```

**Bağımlılık kuralı:** oklar yalnızca aşağı doğru. Core hiçbir zaman UnityEngine'i veya bir SDK'yı bilmez.
Bu, derleyici tarafından zorlanır (`noEngineReferences`).

### Neden iş mantığı motor-bağımsız?
1. **Test edilebilirlik:** para, ödül ve satın alma kuralları Unity açmadan, saniyeler içinde, CI'da test edilir.
2. **SDK değişimi:** reklam/IAP/analytics sağlayıcısı değişince yalnızca adaptör değişir (`IAdsProvider`, `IPurchaseProvider`, `IAnalyticsProvider`, `IRemoteConfigProvider`).
3. **Unity sürüm yükseltmeleri** iş kurallarını etkilemez.

## Composition root
`NovaForgeCore.Build(GameSetup)` tüm servisleri bağımlılık sırasıyla kurar ve `ServiceRegistry`'ye kaydeder.
- DI: reflection'sız, factory/instance tabanlı → IL2CPP ve code stripping güvenli.
- `NovaForgeCore.Validate` kurulumu doğrular; **release build'de** `MockAdsProvider`, `MockPurchaseProvider`
  ve güvensiz validator (`DevelopmentAcceptAllValidator`) **reddedilir**.
- Opsiyonel sistemler: `AdsProvider` yoksa `Ads == null`, `PurchaseProvider` yoksa `Purchases == null`.

## Modüller arası iletişim
- **Doğrudan bağımlılık (constructor injection)**: bir servis diğerinin sonucuna ihtiyaç duyuyorsa (örn. `ShopService → IWallet`).
- **EventBus**: yayın/abone, gevşek bağlılık için (örn. `StatChanged` → görevler ve başarılar; `EntitlementsChanged` → banner gizleme).
  Bir handler'ın exception'ı diğerlerini durdurmaz, `ErrorHandler`'a raporlanır.

## Kalıcılık modeli
- Her servis kendi **slot**'una sahiptir (`economy`, `progress`, `purchases`, `consent`...). Bir slotun bozulması diğerlerini etkilemez.
- Zarf: `{v: şemaVersiyonu, t: zaman, h: sha256(payload), p: payload}`.
- Yükleme sırası: ana dosya → `.bak` → varsayılan (her adım loglanır; kurtarılamayan veri hata olarak raporlanır).
- Yazma atomiktir (`.tmp` + `File.Replace`).
- **Oyunlar ayrık:** depolama kökü `persistentDataPath/novaforge/{gameId}`. Ürün katalogları, analytics ve ekonomi tanımları da oyun başına `GameSetup`'tan gelir.

## Hata politikası
- Oyun akışını kesebilecek her dış çağrı (SDK, ağ, disk) yakalanır, `IErrorHandler.Report` ile loglanır/raporlanır ve
  oyun **devam eder** (reklam yoksa oyun yine oynanır; mağaza yoksa yalnızca satın alma kapalıdır).
- Programcı hataları (yanlış parametre, kilitli bölümü bitirmeye çalışmak) ise exception fırlatır — sessizce yutulmaz.

## Thread modeli
Core servisleri **main thread** için tasarlandı (Unity ile aynı). SDK adaptörleri callback'leri main thread'e taşımakla yükümlüdür.
