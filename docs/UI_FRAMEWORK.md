# UI Framework

Teknoloji: **uGUI + TextMeshPro** (`com.unity.ugui` 2.0, Unity 6 ile gelir). Mobil oyun UI'ı için en olgun yol bu:
prefab akışı, animasyon ve ekosistem desteği geniş. UI Toolkit'in runtime tarafı oyun UI'ı için henüz o kadar olgun değil.

## Katmanlar

| Katman | Tip | Nerede | Test |
|---|---|---|---|
| Navigasyon kuralları | `NavigationService` | `NovaForge.Core` (motor bağımsız) | 10 birim testi (dotnet + Unity) |
| Oyuncu ayarları | `PlayerSettingsService` | `NovaForge.Core` | birim testleri |
| Core metinleri (8 dil) | `CoreStrings` | `NovaForge.Core` | eksik anahtar / placeholder testi |
| Görünüm + geçişler | `UIView`, `UIManager` | `NovaForge.UI` | 5 PlayMode testi |
| Ses + titreşim | `AudioService`, `AudioLibrary`, `Haptics` | `NovaForge.Unity` | crossfade/ayar PlayMode testi; Android/iOS player derlemesi |
| Hazır bileşenler | `ConfirmPopup`, `SettingsView`, `LevelResultView`, `DailyRewardView`, `ShopView`, `LocalizedText`, `CurrencyDisplay`, `UIButtonFeedback` | `NovaForge.UI` | 3 PlayMode testi gerçek Core ile; builder'lar batch'te çalıştırıldı |
| Editor menüsü | `NOVAFORGE ▸ UI ▸ …` | `NovaForge.Editor` | batch'te çalıştırıldı |

## Navigasyon kuralları (`NavigationService`)

- **Ekranlar:** `PushScreenAsync` öncekini geçmişte tutar, `ReplaceScreenAsync` yerine koyar, `ResetToAsync` geçmişi temizler.
- **Popup'lar:** `ShowPopupAsync` hemen üstte açar. `EnqueuePopupAsync` başka popup açık değilken sırayla gösterir:
  önce öncelik, sonra FIFO. Günlük ödül, başarım, teklif popup'ları üst üste binmez; ekran değişince de kuyrukta kalır.
- **Geri tuşu (Android / Escape):** modal olmayan üst popup'ı kapatır. Sonra ekranın kendi `HandleBack`'i, sonra pop,
  en son `ExitRequested` event'i gelir; oyun burada "çıkılsın mı?" diye sorar.
- **Serileştirme:** geçiş sürerken gelen istekler sıraya girer. Geçiş sırasında basılan geri tuşu yutulur.
  Çift dokunma ile ekran atlama ya da üst üste animasyon olmaz.

## Kullanım

```csharp
var nav = UIManager.Instance.Navigation;
await nav.ResetToAsync(ViewIds.MainMenu);
await nav.PushScreenAsync(ViewIds.Shop);
nav.EnqueuePopupAsync(ViewIds.DailyReward, priority: 10).Forget();

bool quit = await UIManager.Instance.ConfirmAsync(new ConfirmRequest
{
    TitleKey = CoreStrings.ExitTitle, MessageKey = CoreStrings.ExitMessage,
});
```

Ekran yazmak için `UIView`'dan türet ve `OnShowing(args)`, `OnShown`, `OnHiding`, `HandleBack` metodlarını override et.
Geçişler `Time.unscaledDeltaTime` ile çalışır, yani oyun duraklatılmışken de oynar. Geçiş boyunca view tıklanamaz.

## Kurulum

1. Menü: **NOVAFORGE ▸ Audio ▸ Create Audio Service** (NovaForgeApp objesine ekler).
1. Proje başına bir kez: **Window ▸ TextMeshPro ▸ Import TMP Essential Resources**.
2. **NOVAFORGE ▸ UI ▸ Create UI Root**: Canvas (1080×1920, match 0.5), SafeArea, Screens, Popups, PopupDim, EventSystem.
3. **Create Confirm Popup** / **Create Settings Popup**: çalışan, bağlantıları kurulmuş varsayılan görünümler. Stil verip prefab yap.
4. Prefab'ları `UIManager.viewPrefabs` listesine ekle; ilk kullanımda oluşturulurlar. Sahnede duran view'lar da otomatik bulunur.

## Hazır oyun ekranları

| View | Id | Ne yapar |
|---|---|---|
| `LevelResultView` | `level_result` | `LevelSessionService.Win/Lose` sonucunu gösterir: yıldızlar, ödül, Sonraki/Tekrar/Ana Menü. "x2 ödül" butonu yalnızca ödül verildiyse ve rewarded reklam hazırsa çıkar. Seçim `LevelResultChosen` event'i olarak yayınlanır; navigasyonu oyun yapar. Geri tuşu = Ana Menü. |
| `DailyRewardView` | `daily_reward` | Config'deki takvimi gösterir, bugünü vurgular, alınanları işaretler, sonraki ödüle geri sayım gösterir. Oturum başında `DailyRewardView.EnqueueIfClaimable()` çağrılır (öncelik 100, kuyrukta ilk sırada). |
| `ShopView` | `shop` | `ShopService` ürünlerini şablon satırdan çoğaltır. Soft-currency ve gerçek para alımlarının ikisi de `ShopService` üzerinden geçer. Yetersiz bakiye, bekleyen ve başarısız alımda bilgi popup'ı açılır; iptalde sessiz kalır. "Satın alımları geri yükle" butonu iOS'ta görünür (Apple şartı). |

Ödül metinleri `resource.<id>` anahtarından gelir, örneğin `"resource.coin": "Altın"`. Anahtar yoksa id gösterilir.

## Ses ve titreşim

- `AudioService`: müzik için iki kaynak var ve parçalar arasında crossfade yapılır. Efektler 10 sesli bir havuzdan çalar; her sesin bekleme süresi var ve havuz doluysa en eski ses kesilir. Ses seviyeleri `PlayerSettings`'i anlık takip eder.
- **Tam ekran reklam sırasında tüm ses susar.** Bunu `FullscreenAdStateChanged` event'i sağlar; iOS'ta reklam SDK'ları Unity sesini kendisi durdurmuyor.
- `AudioLibrary` (ScriptableObject): id'den clip'e tablo; varyasyon ve pitch sapması tanımlanabilir. Core UI şu id'leri kullanır: `ui_click`, `reward_collect`, `level_win`, `level_lose`.
- `Haptics`: ayarlardaki titreşim seçeneğine bağlı.
  - Android'de API 26+ cihazlarda şiddet ayarlı titreşim var.
  - iOS'ta yalnızca güçlü titreşim (`Handheld.Vibrate`) çalışıyor; hafif/orta için native plugin gerekir, dahil değil.
- `UIButtonFeedback`: tıklamada ses, hafif titreşim ve basılma efekti. Builder'lar her butona ekliyor.

## Gizlilik

- `SettingsView`, **Gizlilik ayarları** butonunu yalnızca izin platformu gerektiriyorsa gösterir
  (`GameSetup.PrivacyOptions`, örn. `AdMobAdsProvider`). GDPR, kullanıcının iznini her an değiştirebilmesini istiyor.
- Gizlilik politikası, koşullar ve destek butonları config'den gelir: `privacy.policy_url`, `privacy.terms_url`, `support.email`.
  Yalnızca `https://` linkler açılır.
- Release build'de `privacy.policy_url` boşsa `NovaForgeCore.Validate` hata verir (mağaza şartı).

## Bilinen sınırlar

- Tema/stil sistemi yok: builder'lar uGUI varsayılan sprite'larını kullanır, oyun kendi stilini verir.
- Builder'lar mute toggle'ı üretmez; `SettingsView` alanları opsiyonel olduğu için gerekirse elle bağlanır.
