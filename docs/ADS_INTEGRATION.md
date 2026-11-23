# Reklam Entegrasyonu

## Durum
- Core: `IAdsProvider`, `AdsService`, `AdPolicy`, `MockAdsProvider` — **tamam ve test edildi**.
- Gerçek ağ adaptörü (Google AdMob / Unity LevelPlay / AppLovin MAX): **henüz yok**. Seçim + resmi doküman doğrulaması Unity kurulduktan sonra yapılacak.
- `MockAdsProvider` hiçbir şey göstermez; yalnızca testler içindir ve release build'de `NovaForgeCore.Validate` tarafından reddedilir.

## Adaptör sözleşmesi (`IAdsProvider`)
1. `InitializeAsync(AdPrivacyOptions)`: SDK'yı başlat; `PersonalizedAdsAllowed=false` ise kişiselleştirilmemiş istek yapılandır (AdMob: `npa=1` / UMP sonucu; LevelPlay: consent API), `IsChildDirected` ise ilgili COPPA bayrağı.
2. `ShowRewardedAsync`: SDK'nın **ayrı** gelen "reward earned" ve "closed" callback'lerini tek `AdShowResult`'ta birleştir. `RewardEarned=true` yalnızca SDK ödül callback'i geldiyse.
3. Callback'leri **main thread'e** taşı (Unity main thread dispatcher).
4. `Load*` metotları preload içindir; `AdsService` gösterimden sonra yeniden yükletir.
5. Adaptör kendi başına reklam göstermez, sıklık kararı vermez — bunlar `AdsService`'in işidir.

## Kullanım
```csharp
// Oyuncu "2x ödül" butonuna bastı:
var outcome = await core.Ads.ShowRewardedForBundleAsync(AdPlacements.DoubleReward, RewardBundle.Of("coin", 50), core.Rewards);
if (outcome == RewardedAdOutcome.RewardGranted) ShowRewardPopup();

// Bölüm sonu, sonuç ekranından menüye geçerken:
await core.Ads.TryShowInterstitialAsync(AdPlacements.LevelEnd); // politika izin vermezse hiçbir şey olmaz
```
Butonu yalnızca `core.Ads.IsRewardedAvailable(placement)` true ise aktif göster.

## Uyumluluk notları
- Google Play Families politikası kapsamındaysa yalnızca Families sertifikalı reklam SDK'ları.
- iOS: ATT izni istenmeden IDFA kullanılmaz; `NSUserTrackingUsageDescription` gerekir. AB/UK/İsviçre için Google UMP (IAB TCF) CMP gerekir.
- SKAdNetwork kimlikleri seçilen ağın dokümanından Info.plist'e eklenir.
