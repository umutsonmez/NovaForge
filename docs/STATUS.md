# Proje Durumu ve Yol Haritası

_Son güncelleme: 2026-11-23_

## Ortam
- Unity **6000.3.29f1** (6.3 LTS) + Android (SDK/NDK/OpenJDK) + iOS Build Support kurulu (2026-11-23).
- iOS cihaz build'i ve App Store gönderimi için Mac/Xcode gerekir (Windows yalnızca Xcode projesi üretir).
- .NET SDK 8/10: Unity'siz hızlı doğrulama (`tools/dotnet`).

## Faz 1 — Motor-bağımsız Core ✅ (doğrulandı)
Derleme: 0 uyarı / 0 hata. Testler: **103/103 geçti** — hem Unity Test Runner'da (6000.3.29f1) hem `dotnet test`'te.
Foundation, Config + Remote Config, Save, Economy, Rewards, Entitlements, Ads (kurallar), IAP (akış + sunucu validator istemcisi),
Analytics, Consent, Progression, Level Session, Statistics, Missions, Achievements, Daily Rewards, Tutorial, Difficulty, Shop,
Localization, Object Pool, Composition root (`NovaForgeCore`).

## Faz 1 — Unity köprüsü ✅ derleniyor
`NovaForgeApp`, `UnityLogSink`, `UnityStorage`, `UnityHttpTransport`, `SafeAreaFitter` Unity 6.3'te uyarısız derleniyor.
Çalışma zamanı davranışı için PlayMode testleri henüz yok.

## Reklam ağı — AdMob ✅ derleniyor / ⏳ cihaz testi bekliyor
Karar: AdMob ile başla, ölçeklenince AppLovin MAX ile A/B. `NovaForge.Ads.AdMob` adaptörü `com.google.ads.mobile` 11.7.0 ile
uyarısız derleniyor. Gerçek cihazda (Android cihaz/emülatör) test reklamları + UMP formu ile denenmesi gerekiyor.

## Faz 3a — UI framework ✅ (doğrulandı)
Detay: [UI_FRAMEWORK.md](UI_FRAMEWORK.md). `NavigationService` (ekran/popup yığını, popup kuyruğu, geri tuşu, serileştirme),
`PlayerSettingsService`, 8 dilde `CoreStrings`, `UIView`/`UIManager`, `ConfirmPopup`, `SettingsView`, `LocalizedText`,
`CurrencyDisplay`, `NOVAFORGE ▸ UI` editor menüsü.

## Faz 3b — Hazır ekranlar + ses ✅ (doğrulandı)
`LevelResultView`, `DailyRewardView`, `ShopView`, `UIButtonFeedback`, `AudioService` (crossfade, SFX havuzu, reklam sırasında susma), `Haptics`.
Testler: EditMode 118/118, PlayMode 8/8, dotnet 118/118. Android + iOS **player script derlemesi** temiz (AdMob dahil).
Cihazda henüz çalıştırılmadı: titreşim, gerçek reklam, gerçek ses çıkışı.

## Faz 7 — Referans oyun Tiltwell ✅ oynanabilir (editörde doğrulandı)
Detay: [TILTWELL.md](TILTWELL.md). 36 solver-doğrulamalı bölüm, ana menü, bölüm seçimi, oyun ekranı (swipe, geri al, yeniden başlat, ipucu,
ekstra hamle reklamı), sonuç/günlük ödül/mağaza/ayarlar Core ekranlarından. Sahne builder ile üretiliyor.
Testler: EditMode 141/141, PlayMode 10/10 (uçtan uca bölüm çözme dahil), dotnet 118 + 22. Android + iOS player derlemesi temiz.
Eksik: ses dosyaları, ikon/mağaza görselleri, JP/KR font, cihaz testi.

## Sıradaki fazlar (yapılmadı)
| Faz | İçerik | Ön koşul |
|---|---|---|
| 2 | ✅ Sandbox projesi, Core testleri Unity'de, Unity köprüsü + AdMob derlemesi | — |
| 3 | ✅ UI + hazır ekranlar + ses. Kalan: tema, (pooling, mixer), Input (tap/swipe/drag/long-press), Scene transitions, Addressables | Faz 2 |
| 4 | SDK adaptörleri: Unity IAP 5.x, AdMob cihaz testi, Firebase Analytics/Remote Config/Crashlytics, Google UMP + iOS ATT | Faz 2 + hesaplar |
| 5 | Editor araçları: NOVAFORGE menüsü, Create New Game, validators (config/monetization/localization), Level Editor, build config | Faz 3 |
| 6 | Oyun şablonları (Puzzle, Arcade, Hypercasual, Idle, Simulation) | Faz 3-5 |
| 7 | ✅ Tiltwell oynanabilir. Kalan: ses, görseller, cihaz testi, mağaza materyali | Faz 3-5 |
| 8 | Satın alma doğrulama backend'i, sandbox testleri, BUILD_AND_RELEASE süreci | Faz 4 |

Henüz yazılmayan dokümanlar (ilgili sistemle birlikte gelecek; boş placeholder üretilmedi):
`GAME_CREATION_GUIDE.md`, `LEVEL_EDITOR_GUIDE.md`, `BUILD_AND_RELEASE.md`.

## Bilinen sınırlar
- Save checksum'ı bozulmayı tespit eder, hileye karşı koruma değildir.
- Günlük ödül ve reklam günlük limitleri cihaz saatine dayanır (geri alma tespiti var); değerli ödüller için sunucu saati önerilir.
- `AdsService` ödüllü reklam günlük sayacı bellekte tutulur (uygulama yeniden başlayınca sıfırlanır); kalıcı sınır için `EconomyDefinition.AddDailyEarnLimit` kullanılmalı.
- `ICloudSaveProvider` yalnızca arayüz.
- Satın alma doğrulama sunucusu yok; release build'de `DevelopmentAcceptAllValidator` engellenir.
