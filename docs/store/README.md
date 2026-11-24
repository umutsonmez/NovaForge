# Tiltwell: mağaza görselleri ve metinleri

## Dosyalar
| Dosya | Play Console alanı | Boyut |
|---|---|---|
| `icon-512.png` | Uygulama simgesi | 512 × 512 PNG |
| `feature-1024x500.png` | Öne çıkan grafik | 1024 × 500 PNG |
| `screenshots-tr/*.png` | Telefon ekran görüntüleri (Türkçe arayüz) | 1080 × 1920 |

Play Console şunları istiyor:
- Simge: 512 × 512 boyutunda, 32 bit PNG ve en fazla 1 MB.
- Öne çıkan grafik: 1024 × 500 boyutunda.
- Ekran görüntüleri: en az 2 tane, kenarlar 320 ile 3840 px arasında ve en-boy oranı en fazla 2:1.

Yükleme öncesi Play Console'daki güncel kurallara bir kez daha bakın.

## Yeniden üretme
- **Simge ve öne çıkan grafik:** Unity'de **NOVAFORGE ▸ Tiltwell ▸ Regenerate App Icon and Store Art** (`TiltwellArtBuilder`) çalıştırılır.
  - Oyunun kendi damla ve tahta çizimiyle üretir.
  - Simgeyi `Assets/Tiltwell/Art/AppIcon.png` olarak kaydeder ve varsayılan oyuncu simgesi yapar.
  - Android adaptif simge katmanları ayrıca verilmediği için Unity bu simgeyi kullanır.
- **Ekran görüntüleri:** geçici bir PlayMode yakalama testiyle 1080 × 1920 boyutunda alındı. Başka bir dil için cihaz dilini değiştirip tekrar alın.

## Mağaza metni (taslak)
**Kısa açıklama (TR, en fazla 80 karakter):** Tahtayı eğ, damlaları karıştır, doğru renkleri kuyulara ulaştır!

**Short description (EN):** Tilt the board, mix the drops, and guide every color home!

**Uzun açıklama (TR):**
Tiltwell rahatlatıcı bir renk bulmacası. Tahtayı dört yöne eğdiğinde bütün damlalar birlikte kayar. Çarpışan damlalar karışır: kırmızı ile sarı turuncu, kırmızı ile mavi mor, sarı ile mavi yeşil olur. Her damlayı kendi renginin kuyusuna ulaştır.

- 10 dünyada 92 el yapımı bölüm. Yapışkan bal, yön okları ve boya karoları gibi yeni mekanikler var.
- Her gün yeni bir bulmaca ve seri ödülleri.
- Dünyayı bitirince açılan hazine sandıkları.
- Gözlü, jöle, baloncuk ve yıldız damla görünümleri.
- Takıldığında ipucu ve geri alma. Zaman baskısı yok.
