# Monetization Rehberi

## İlkeler (kodla zorlanır)
| Kural | Nerede |
|---|---|
| Ödüllü reklam yalnızca oyuncu isteğiyle | `AdsService.ShowRewardedAsync` sadece buton aksiyonundan çağrılır; otomatik gösterim API'si yok |
| İzlenmeden ödül yok | Ödül yalnızca ağ `RewardEarned=true` dönerse |
| Aynı reklamdan çift ödül yok | İstek başına benzersiz id → `RewardService` idempotent ledger |
| Reklam sıklığı merkezi | `AdPolicy` (remote config) — onboarding, bölüm aralığı, süre aralığı, ödüllü sonrası grace, oturum limiti |
| Reklamsız paketle çelişki yok | `no_ads` → interstitial/banner/app-open kapalı, aktif banner gizlenir; ödüllü (opt-in) açık |
| Reklam yoksa oyun devam eder | Tüm çağrılar sonuç döner, exception sızmaz |
| İzinler | `ConsentService` → kişiselleştirme bayrakları sağlayıcıya iletilir; ATT reddi/belirsizliği = kişiselleştirilmemiş |
| Gerçek para doğrulanmadan teslim yok | `PurchaseService` → `IPurchaseValidator` (production: sunucu) |
| Fiyat kodda yok | `StoreProductInfo.LocalizedPrice` mağazadan |

## Ekonomi tasarımı
- Oyun başına `EconomyDefinition`. Önerilen başlangıç: `coin` (soft), opsiyonel `gem` (hard), item'lar (`hint`, `extra_moves`).
- Reklam kaynaklı kazanca günlük limit koy: `AddDailyEarnLimit("coin", "rewarded_ad", N)`.
- Ödül miktarları ve takvim remote config'te (`daily_rewards.schedule`, oyunun kendi anahtarları).

## Önerilen başlangıç politikası (casual puzzle)
- İlk 3 bölümde interstitial yok, sonra en az 2 bölümde bir ve 90 sn aralık.
- Banner kapalı (varsayılan).
- Ödüllü: "2x ödül", "+5 hamle", "ücretsiz ipucu" yerleşimleri.
- IAP: Remove Ads (non-consumable), 3 coin paketi, 1 starter pack (tek seferlik).

Bu değerler başlangıç hipotezidir; A/B testi ve metriklerle (ARPDAU, D1/D7 retention, ad engagement) ayarlanmalı.
