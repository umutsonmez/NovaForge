# Uygulama İçi Satın Alma (IAP)

## Durum
- Core: `IPurchaseProvider`, `PurchaseService`, `ServerPurchaseValidator`, `EntitlementService`, `MockPurchaseProvider` — **tamam ve test edildi**.
- Unity IAP adaptörü (`com.unity.purchasing` 5.x, `StoreController` API'si): **henüz yazılmadı** — Unity kurulumu gerekli.
- Doğrulama backend'i: **yok**. `ServerPurchaseValidator`'ın konuşacağı sunucu sözleşmesi aşağıda.

## Akış
```
Store ──PurchaseReceived──▶ PurchaseService
                              │ ledger'da var mı? ── evet ─▶ FinishTransaction (çift teslim yok)
                              ▼ hayır
                         IPurchaseValidator
            Valid ──▶ Fulfill (economy tx "iap:{txId}" + entitlement) ──▶ ledger kaydet ──▶ FinishTransaction
            Invalid ──▶ teslim yok, FinishTransaction, hata raporu
            RetryLater ──▶ FINISH YOK (mağaza bir sonraki açılışta yeniden iletir)
```
Ledger kaydı finish'ten **önce** yapılır: aradaki crash'te mağaza işlemi tekrar iletir, ledger tanır, çift ödeme olmaz.

## Ürün tanımı
```csharp
setup.Products.Add(new ProductDefinition("coins_small", StoreProductType.Consumable, RewardBundle.Of("coin", 500),
    storeIds: new Dictionary<string,string>{{"google","com.studio.game.coins_small"},{"apple","com.studio.game.coins_small"}}));
setup.Products.Add(new ProductDefinition("remove_ads", StoreProductType.NonConsumable, null, StandardEntitlements.NoAds));
```
Fiyatlar Google Play Console / App Store Connect'te tanımlanır; uygulama `StoreProductInfo.LocalizedPrice` gösterir.

## Sunucu doğrulama sözleşmesi
`POST https://<backend>/validate`
```json
{"gameId":"...","platform":"google|apple","productId":"...","transactionId":"...","receipt":"<opaque>"}
```
Cevap `200`: `{"status":"valid","productId":"...","expiresAtMs":null}` veya `{"status":"invalid","reason":"..."}`.
`5xx`/`429`/ağ hatası → istemci tekrar dener; `4xx` → geçersiz.

Sunucu tarafı (yapılacak): Google Play Developer API (`purchases.products.get`, `purchases.subscriptionsv2.get`) ve
Apple App Store Server API (`Get Transaction Info`) ile doğrular; transactionId'yi sunucuda da tekil tutar; servis hesabı
anahtarları **asla** uygulamaya gömülmez.

## Restore
Apple, non-consumable/abonelik için görünür "Satın Alımları Geri Yükle" butonu ister: `core.Purchases.RestoreAsync()`.
Restore edilen işlemler revenue olarak raporlanmaz.

## Sandbox testi (yapılacak, Unity adaptöründen sonra)
- Google: Play Console → License testers + internal testing track.
- Apple: App Store Connect → Sandbox tester hesapları; StoreKit Configuration dosyası ile Xcode'da yerel test.
