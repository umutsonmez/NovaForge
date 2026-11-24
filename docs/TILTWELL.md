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

## Özel zeminler (Dünya 6+)
- `~` **yapışkan** (bal rengi): üstüne giren damla orada durur, sonraki eğişte çıkabilir.
- `^ v < >` **tek yön oku**: sadece ok yönünde girilebilir, diğer yönlerden duvar gibi davranır.
- `1 2 3` **boya karosu** (elmas): üstünden geçen damlaya kırmızı/sarı/mavi ekler.
- Dağılım: Dünya 6 yapışkan, 7 kahverengi kuyu, 8 ok, 9 boya, 10 hepsi. Her mekaniğin ilk bölümünde kısa açıklama mesajı çıkar.
- Solver bunları bilir. `CanStillSolve`, boya karolarının ekleyebileceği renkleri de hesaba katar.

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
- **Renk öğretimi:**
  - Bölüm 1'de ve karışım gereken ilk bölümde her hamlede bedava yön oku çıkar.
  - Karışım gereken ilk bölümde bir kez renk rehberi (`color_guide` popup'ı) açılır: kırmızı+sarı=turuncu, kırmızı+mavi=mor, sarı+mavi=yeşil.
  - Kahverengi ilk çıktığında rehber kahverengi satırıyla tekrar açılır.
  - Kademe 4'e kadar (ve kahverengi dünyasında) oyun ekranının üstünde, o bölümün tariflerini gösteren bir çubuk durur; dokununca rehber açılır.
  - Rehberin "bir kez" gösterilmesi Core `TutorialService` ile kaydedilir (`color_mix`, `color_brown`).
- Sonuç, günlük ödül, mağaza, ayarlar Core'un temalı ekranları (konfeti, sırayla yıldız).

## Bölümler
`Runtime/Levels/levels.json`: 92 bölüm, 10 kademe (dünya), seed `20261123`. Kademe 6+ sonradan eklendi; ilk 36 bölüm aynı kalır (kayıtlı ilerleme bozulmaz). Kademe 7'den itibaren kahverengi kuyular var (üç ana rengin hepsi gerekir). Kademe 6, 8, 9 ve 10 özel zeminler kullanır. Her bölüm BFS solver ile çözülebilirliği ve par değeri doğrulanmış olarak üretilir:
```
cd tools/dotnet/TiltwellGen && dotnet run -c Release -- 20261123 ../../../Packages/com.novaforge.tiltwell/Runtime/Levels/levels.json
```
`LevelPackTests` gönderilen her bölümü solver ile yeniden çözer; par tutmazsa test kırılır.

## Ses ve titreşim
- Tüm sesler kodla sentezlenir (`Editor/TiltwellSoundBuilder`, menü NOVAFORGE ▸ Tiltwell ▸ Regenerate Sounds). Çıktı `Runtime/Audio/*.wav` ve `TiltwellAudio.asset`; üçüncü parti ses yok, lisans derdi yok.
- Efektler: tıklama, popup, kayma, duvara çarpma, birleşme, kuyuya girme, deliğe düşme, coin, kazanma, kaybetme. Müzik: 20 sn'lik dikişsiz döngü (C–Am–F–G, 96 bpm).
- Titreşim: damlalar durunca hafif; kuyu veya delik olunca orta. Ayarlardaki Titreşim anahtarına bağlı.
- Sesler hoparlörde dinlenmedi; dalga formu (tepe, tıklama, döngü dikişi) script ile kontrol edildi.

## Reklam
`AdMobSettings` atanırsa AdMob (UMP consent köprüsü `OnCoreBuilt` içinde bağlanır); atanmazsa development build'de `MockAdsProvider`, release'de reklam yok.
Release build için `privacyPolicyUrl` (https) zorunlu; yoksa Core doğrulaması başlatmayı durdurur.

## Testler
- dotnet: 28 test (kural, solver, oturum, renk tarifi, özel zemin).
- Unity PlayMode `TiltwellPlayTests`: gerçek sahne; günlük ödül popup'ı → Play → bölüm 1'i solver hamleleriyle çöz → 3 yıldız + coin → Next ile bölüm 2. Ayrıca geri al ve ipucu harcama.

## Eksikler (dürüst liste)
- Uygulama ikonu, splash ve mağaza görselleri yok.
- Japonca/Korece karakterler Nunito'da yok (Noto fallback gerekli).
