# Reklam Entegrasyonu

## Karar
**Google AdMob** (Google Mobile Ads Unity plugin 11.x) ile başlanır. Trafik oluştuktan sonra AppLovin MAX adaptörü eklenip
A/B ile karşılaştırılacak. Değişim yalnızca `IAdsProvider` adaptörü değiştirerek yapılır; oyun kodu etkilenmez.

## Durum
| Parça | Durum |
|---|---|
| Core: `IAdsProvider`, `AdsService`, `AdPolicy`, `MockAdsProvider` | ✅ tamam, test edildi |
| `NovaForge.Ads.AdMob` adaptörü (`Runtime/Adapters/AdMob`) | ✅ `com.google.ads.mobile` 11.7.0 + Unity 6000.3.29f1 ile uyarısız derleniyor |
| Cihaz testi (test reklamları, UMP formu, ATT) | ❌ Unity + cihaz gerekli |

Adaptör asmdef'i yalnızca `com.google.ads.mobile` ≥ 11.0.0 kuruluysa derlenir (`versionDefines` → `NOVAFORGE_ADMOB`).
Paket kurulu değilse adaptör yok sayılır, projenin geri kalanı etkilenmez.

Derlemede doğrulandı: `GoogleMobileAds.Ump.Api` namespace'i, `BannerView.Show()/Hide()`, `ConsentInformation.Update` callback imzası.
Not: plugin 11.7'de `MobileAds.RaiseAdEventsOnUnityMainThread` **obsolete**; adaptör her callback'i
`MobileAdsEventExecutor.ExecuteInUpdate` ile main thread'e taşır.

## Kurulum (oyun projesinde)
1. OpenUPM üzerinden (Google'ın önerdiği yöntem) — `Packages/manifest.json`:
   ```json
   "scopedRegistries": [{ "name": "OpenUPM", "url": "https://package.openupm.com", "scopes": ["com.google"] }],
   "dependencies": { "com.google.ads.mobile": "11.7.0" }
   ```
   `.unitypackage` ile kurulumda `versionDefines` çalışmaz; o durumda Player Settings → Scripting Define Symbols'e `NOVAFORGE_ADMOB` elle eklenir.
2. **Assets > Google Mobile Ads > Settings** → Android ve iOS **AdMob App ID** (AdMob konsolundan).
3. **Create > NovaForge > Ads > AdMob Settings** → production ad unit ID'leri (rewarded, interstitial, banner).
4. Android: minimum API 24, target API 35. iOS: Xcode 16+, iOS 13+, CocoaPods.
5. İsteğe bağlı: aynı Settings ekranından "Upgrade to the GMA Next-Gen SDK" (Android, plugin 11.1+).

## Bootstrap'te bağlama
```csharp
protected override GameSetup CreateSetup()
{
    var setup = new GameSetup { /* ... */ };
    _admob = new AdMobAdsProvider(adMobSettings, isReleaseBuild: !Debug.isDebugBuild, log: ...);
    setup.AdsProvider = _admob;
    return setup;
}

protected override void OnCoreReady(NovaForgeCore core)
{
    AdMobConsentBridge.Connect(_admob, core.Consent);   // not: InitializeAsync'ten önce bağlamak için Awake sonrası, Start öncesi de yapılabilir
    _admob.AdRevenuePaid += (format, micros, currency) =>
        core.Analytics.Log(AnalyticsEvents.AdImpression, new Dictionary<string, object> {
            { AnalyticsParams.AdFormat, format.ToString() }, { AnalyticsParams.Value, micros / 1_000_000.0 }, { AnalyticsParams.Currency, currency } });
}
```
> `ConsentResolved`, `InitializeAsync` sırasında tetiklenir. Kaçırmamak için `Connect`, `core.InitializeAsync()` çağrısından **önce**
> yapılmalı. Bunun için `NovaForgeApp`'e bir "Core oluşturuldu, init öncesi" kancası eklenecek (Faz 2'de, Unity içinde test ederek).

## Adaptörün garantileri
- **İzin sırası (Google UMP):** `ConsentInformation.Update` → gerekiyorsa form → `CanRequestAds()` → `RequestConfiguration` → `MobileAds.Initialize`.
  `CanRequestAds()` false ise hiç reklam istenmez; oyun reklamsız devam eder.
- **Privacy options:** GDPR bölgelerinde `PrivacyOptionsRequired == true` olur. Oyunun ayarlar ekranında "Gizlilik ayarları"
  butonu gösterilip `ShowPrivacyOptionsFormAsync()` çağrılmalı (zorunlu).
- **Çocuk kullanıcı:** `ConsentService.IsChildDirected` → `AgeRestrictedTreatment.Child`. Eski `TagForChildDirectedTreatment` deprecated olduğu için kullanılmaz.
- **Ödül:** `Show(reward => ...)` callback'i + `OnAdFullScreenContentClosed` tek `AdShowResult`'ta birleşir. Ödül yalnızca reward callback geldiyse verilir.
- **Tek kullanımlık nesneler:** her gösterimden sonra `Destroy()` çağrılır, yenisi yüklenir.
- **Yükleme hatası:** üstel geri çekilme (2→64 sn).
- **Main thread:** tüm SDK callback'leri `MobileAdsEventExecutor.ExecuteInUpdate` ile Unity main thread'ine taşınır.
- **Test / production ID koruması:**
  - Development build: Google test ad unit'leri kullanılır. Geliştirirken gerçek reklama tıklamak AdMob hesabının kapatılma sebebidir.
  - Release build: test ID'si tespit edilirse uygulama açılışta hata verir. Yani gelir getirmeyen bir sürüm sessizce yayına çıkamaz.

## Google test ad unit ID'leri
Kaynak: https://developers.google.com/admob/unity/test-ads

| Format | Android | iOS |
|---|---|---|
| Rewarded | `ca-app-pub-3940256099942544/5224354917` | `ca-app-pub-3940256099942544/1712485313` |
| Interstitial | `ca-app-pub-3940256099942544/1033173712` | `ca-app-pub-3940256099942544/4411468910` |
| Banner | `ca-app-pub-3940256099942544/6300978111` | `ca-app-pub-3940256099942544/2934735716` |

## Yayın öncesi kontrol listesi
- [x] AdMob hesabı, uygulama kaydı, ad unit'ler (Tiltwell Android: 2026-11-24)
- [x] `app-ads.txt` geliştirici web sitesinde yayında (https://umutsonmez.github.io/app-ads.txt; Play'de web sitesi bu alan adı olmalı)
- [x] AdMob → Privacy & messaging: GDPR mesajı (UMP) oluşturuldu ve yayında
- [ ] iOS: `NSUserTrackingUsageDescription`, ATT istemi, SKAdNetwork ID'leri (AdMob dokümanındaki liste)
- [ ] Google Play Data safety formu ve App Store Privacy Nutrition Label reklam SDK'sına göre dolduruldu
- [ ] Çocuklara yönelik ise: Families politikası, `AgeRestrictedTreatment.Child`, `MaxAdContentRating.G`
- [ ] Gerçek cihazda test reklamlarıyla tüm yerleşimler denendi

## Kullanım (oyun kodu)
```csharp
var outcome = await core.Ads.ShowRewardedForBundleAsync(AdPlacements.DoubleReward, RewardBundle.Of("coin", 50), core.Rewards);
await core.Ads.TryShowInterstitialAsync(AdPlacements.LevelEnd); // politika izin vermezse hiçbir şey olmaz
```
