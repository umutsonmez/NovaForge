# Google Play yayın rehberi (Tiltwell)

## 1. Yükleme anahtarı (bir kez)
Anahtar **repo dışında** saklanır (`.gitignore` zaten `*.keystore` ve `*.jks` dosyalarını dışlar). Şifreyi bir şifre yöneticisine kaydet ve dosyanın yedeğini al.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.29f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe" `
  -genkeypair -keystore "$HOME\keys\tiltwell-upload.jks" -alias upload -keyalg RSA -keysize 2048 -validity 10000
```
Play Console'da **Play App Signing** açık kalır. Google asıl imza anahtarını kendisi saklar, bu dosya yalnızca yükleme anahtarıdır. Kaybolursa Play Console'dan sıfırlama istenebilir.

## 2. AAB derleme
```powershell
$env:NF_KEYSTORE = "$HOME\keys\tiltwell-upload.jks"
$env:NF_KEYSTORE_PASS = "<şifre>"; $env:NF_KEY_ALIAS = "upload"; $env:NF_KEY_PASS = "<şifre>"
# isteğe bağlı: $env:NF_VERSION_NAME = "1.0.1"; $env:NF_VERSION_CODE = "2"
& "C:\Program Files\Unity\Hub\Editor\6000.3.29f1\Editor\Unity.exe" -batchmode -quit `
  -projectPath Projects\Sandbox -executeMethod NovaForge.Tiltwell.Editor.TiltwellReleaseBuild.BuildAab -logFile release.log
```
Editörden de çalıştırılabilir: **NOVAFORGE ▸ Tiltwell ▸ Build Release AAB**. Ortam değişkenlerinin editör açılmadan önce tanımlanmış olması gerekir.

- **Çıktı:** `Projects/Sandbox/Builds/Release/Tiltwell-<versionName>-<versionCode>.aab`
- **Derleme ayarları:** IL2CPP, ARM64 ve ARMv7, hedef SDK otomatik (36), geliştirme modu kapalı.
- **versionCode:** Her derlemede 1 artar; başarısız derlemede geri alınır.
- **Şifreler:** Yalnızca derleme süresince ayarlanır, sonra ProjectSettings'ten silinir.

## 3. Play Console sırası (kişisel hesap)
1. **Uygulama oluştur:** ad Tiltwell, oyun, ücretsiz, varsayılan dil Türkçe.
2. **Test ▸ Dahili test:** AAB'yi yükle ve kendi telefonunda dene.
3. **Uygulama içeriği:**
   - Gizlilik politikası: https://umutsonmez.github.io/NovaForge/tiltwell/privacy.html
   - Reklam içerir: evet. Reklam kimliği kullanımı: evet, reklam amaçlı.
   - Hedef kitle: 13+ (çocukları seçme).
   - İçerik derecelendirmesi: IARC anketi.
   - Veri güvenliği: AdMob reklam kimliği, cihaz kimliği, yaklaşık konum ve uygulama etkileşimleri. Amacı reklam ve analiz; veriler üçüncü tarafla paylaşılıyor, aktarımda şifreli.
   - Giriş gerekmez.
4. **Mağaza sayfası:** `docs/store/` içindeki görseller ve metinler.
5. **Test ▸ Kapalı test:** en az **12 testçi**, **14 gün kesintisiz** katılımla. Sonra Pano'dan üretim erişimine başvurulur.
6. **Üretim:** onaydan sonra kademeli yayın (ör. %20).

## Notlar
- Yayın sürümünde gerçek reklamlar çıkar. **Kendi reklamına tıklama**; AdMob hesabı kapanabilir. Kendi cihazını AdMob'a test cihazı olarak ekle.
- Paket adı `com.umutsonmez.tiltwell` ilk yüklemeden sonra değiştirilemez.
