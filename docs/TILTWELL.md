# Tiltwell — referans oyun

`Packages/com.novaforge.tiltwell`. NOVAFORGE Core üzerine kurulu, mağazaya çıkabilir kalitede hedeflenen orijinal bulmaca oyunu.
Amaç: platformun her modülünü (ekonomi, ilerleme, günlük ödül, mağaza, reklam, UI, ses, yerelleştirme) gerçek bir oyunda kanıtlamak.

## Kurallar
- Tahtayı eğ (swipe / ok tuşları): tüm damlalar duvara çarpana kadar kayar; duvara yakın olan önce hareket eder.
- Kayan damla duran damlaya çarparsa birleşir; renkler karışır (Kırmızı+Sarı=Turuncu, Sarı+Mavi=Yeşil, Kırmızı+Mavi=Mor, üçü=Kahverengi).
- Delik (`x`) damlayı yutar.
- Hareket bitince tam kendi rengindeki açık kuyuya oturan damla kuyuyu doldurur. Tüm kuyular dolunca bölüm biter.
- Yıldız: hamle ≤ par → 3, ≤ par+3 → 2, aksi 1. Hamle limiti = par×2+4 (ödüllü reklamla +5).
- Geri al, yeniden başlat, ipucu (ipucu eşyası harcar; yoksa ödüllü reklam).

## Yapı
| Katman | Asmdef | Not |
|---|---|---|
| Kurallar, solver, üretici, `PuzzleSession` | `NovaForge.Tiltwell.Logic` | Motor bağımsız; dotnet ile test edilir |
| Görünümler, uygulama | `NovaForge.Tiltwell` | `TiltwellApp`, `GameplayView`, `MainMenuView`, `LevelSelectView`, `BoardView`, `SwipeInput` |
| Sahne üretici | `NovaForge.Tiltwell.Editor` | NOVAFORGE ▸ Tiltwell ▸ Build Game Scene |

Sahne elle yapılmaz; builder koddan üretir (`Assets/Tiltwell/Tiltwell.unity`) ve tek build sahnesi yapar.

## Bölümler
`Runtime/Levels/levels.json`: 36 bölüm, 5 kademe, seed `20261123`. Her bölüm BFS solver ile çözülebilirliği ve par değeri doğrulanmış olarak üretilir:
```
cd tools/dotnet/TiltwellGen && dotnet run -c Release -- 20261123 ../../../Packages/com.novaforge.tiltwell/Runtime/Levels/levels.json
```
`LevelPackTests` gönderilen her bölümü solver ile yeniden çözer; par tutmazsa test kırılır.

## Reklam
`AdMobSettings` atanırsa AdMob (UMP consent köprüsü `OnCoreBuilt` içinde bağlanır); atanmazsa development build'de `MockAdsProvider`, release'de reklam yok.
Release build için `privacyPolicyUrl` (https) zorunlu; yoksa Core doğrulaması başlatmayı durdurur.

## Testler
- dotnet: 22 kural/solver/oturum testi.
- Unity PlayMode `TiltwellPlayTests`: gerçek sahne; günlük ödül popup'ı → Play → bölüm 1'i solver hamleleriyle çöz → 3 yıldız + coin → Next ile bölüm 2. Ayrıca geri al ve ipucu harcama.

## Eksikler (dürüst liste)
- Ses dosyası yok: `AudioLibrary` atanmamış, oyun sessiz çalışır.
- Görseller koddan üretilen daire/yuvarlak kare sprite'lar; ikon, splash ve mağaza görselleri yok.
- Japonca/Korece karakterler varsayılan TMP fontunda yok (Noto fallback gerekli).
- Cihazda oynanmadı; dokunmatik his ve performans doğrulanmadı.
