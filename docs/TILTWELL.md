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
| Görünümler, uygulama | `NovaForge.Tiltwell` | `TiltwellApp`, `GameplayView`, `MainMenuView`, `LevelMapView` + `LevelMapNode`, `BoardView`, `SwipeInput` |
| Sahne üretici | `NovaForge.Tiltwell.Editor` | NOVAFORGE ▸ Tiltwell ▸ Build Game Scene |

Sahne elle yapılmaz; builder koddan üretir (`Assets/Tiltwell/Tiltwell.unity`) ve tek build sahnesi yapar.

## Ekranlar
- **Ana menü:** para/ipucu chip'leri, ayarlar, gradyanlı logo, süzülen damlalar, eğik mini tahta, nefes alan "Oyna · Bölüm N", alt sırada Bölümler / Mağaza / Günlük Ödül.
- **Bölüm haritası (`levels`):** alttan yukarı kıvrılan yol (`UIPolyline`). Geçilen kısım altın, kalan kesik çizgi. Her kademe ayrı renkli "Dünya N" bölgesi ve afişi.
  Kilitli bölüm gri + kilit, geçilen bölüm yıldızlı, aktif bölümde nabız halkası + süzülen işaretçi. Açılışta aktif bölüme kayar.
- **Oyun:** beyaz panelli tahta, kabarık duvarlar, derin delikler, parlayan kuyular, parlak/gölgeli damlalar, kayarken esneme,
  birleşme/teslimde parçacık. Üstte ev, bölüm ve hamle chip'leri, rozetli ipucu; altta geri al / baştan; mesaj balonu.
- Sonuç, günlük ödül, mağaza, ayarlar Core'un temalı ekranları (konfeti, sırayla yıldız).

## Bölümler
`Runtime/Levels/levels.json`: 92 bölüm, 10 kademe (dünya), seed `20261123`. Kademe 6+ sonradan eklendi; ilk 36 bölüm aynı kalır (kayıtlı ilerleme bozulmaz). Kademe 7'den itibaren kahverengi kuyular var (üç ana rengin hepsi gerekir). Her bölüm BFS solver ile çözülebilirliği ve par değeri doğrulanmış olarak üretilir:
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
- Uygulama ikonu, splash ve mağaza görselleri yok.
- Japonca/Korece karakterler Nunito'da yok (Noto fallback gerekli).
- Cihazda oynanmadı; dokunmatik his ve performans doğrulanmadı.
