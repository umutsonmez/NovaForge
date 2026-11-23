# Test Rehberi

## Çalıştırma
**Unity olmadan (CI dahil):**
```bash
cd tools/dotnet && dotnet test NovaForge.slnx
```
**Unity içinde:** Window → General → Test Runner → EditMode → `NovaForge.Core.Tests`.
(Unity Editor'da ilk çalıştırma henüz yapılmadı — bkz. STATUS.md.)

## Kapsam (103 test)
| Alan | Örnek senaryolar |
|---|---|
| Foundation | DI döngü tespiti, dispose sırası, event handler izolasyonu, FSM geçiş reddi/kuyruk |
| Config | aralık dışı / tip hatalı / bozuk JSON reddi, interstitial taban değeri, remote timeout/exception, yalnızca geçerli değerlerin cache'lenmesi |
| Save | round-trip (Unicode), backup'tan kurtarma, checksum, migration, eksik migration, yeni sürüm kaydı, path traversal, atomik dosya yazma, oyun izolasyonu |
| Economy | negatif bakiye, atomiklik, restart sonrası duplicate, max, overflow, günlük limit, kayıt hatasında rollback |
| Ads | tek ödül, atlanan/başarısız reklam, provider exception, günlük limit, no_ads, onboarding/aralık/oturum limitleri, consent/ATT |
| IAP | teslim + finish, crash sonrası yeniden iletim, iptal, deferred, geçersiz makbuz, validator erişilemez → retry, ürün uyuşmazlığı, restore, HTTP eşlemesi |
| Oyun sistemleri | kilit açma, yıldız, level oturumu ödülleri, günlük ödül/seri/saat hilesi, görev sıfırlama, başarı, tutorial |
| Localization | geri düşme zinciri, kültür çözümleme, eksik anahtar / placeholder uyuşmazlığı |
| Composition | uçtan uca oyna-kazan-harca-satın al-yeniden başlat; release build'de mock reddi |

## Mock'lar
`MockAdsProvider`, `MockPurchaseProvider`, `StaticRemoteConfigProvider`, `InMemorySaveStorage`, `ManualClock`.
Mock'lar yalnızca davranış simülasyonudur; gerçek SDK testi yerine geçmez.

## Henüz olmayanlar
- PlayMode testleri (UI, sahne geçişleri) — Unity katmanıyla birlikte.
- Gerçek mağaza sandbox testleri — Unity IAP adaptöründen sonra (IAP_INTEGRATION.md).
