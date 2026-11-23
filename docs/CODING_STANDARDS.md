# Kodlama Standartları

## Dil ve derleyici
- C# 9, netstandard2.1 uyumlu (Unity kısıtı). `record`, `init`, nullable reference types kullanılmaz.
- Uyarılar hata sayılır (`tools/dotnet/Directory.Build.props`).

## Mimari kurallar
- İş kuralı içeren her kod `NovaForge.Core`'a girer; Unity tiplerine referans yasak (derleyici zorlar).
- Reflection tabanlı DI/serileştirme yok (IL2CPP stripping). JSON: Newtonsoft, `TypeNameHandling.None`.
- Dış SDK'lar yalnızca adaptör arayüzleri arkasında; oyun kodu SDK tiplerini görmez.
- Constructor injection; servisler `null` bağımlılığı `ArgumentNullException` ile reddeder.
- Zaman: daima `IClock`. `DateTime.Now` yasak (yerel saat + test edilemez).
- Kimlikler (`gameId`, kaynak, ürün, save anahtarı): `^[a-z0-9][a-z0-9_.-]{0,63}$`.

## Hata yönetimi
- Programcı hatası → exception. Çevresel hata (ağ, SDK, disk) → yakala, `IErrorHandler.Report`, güvenli varsayılanla devam.
- `catch {}` (boş) yasak. Yutulan her exception loglanır veya raporlanır.
- Para/ödül işlemleri sonuç enum'u döndürür (`TransactionStatus`), exception değil.

## Kullanıcı metni
- Kullanıcıya görünen hiçbir metin kodda sabit yazılmaz; `ILocalization.Get(key)`. Anahtar biçimi: `ekran.öğe` (`shop.title`).

## İsimlendirme
- PascalCase tipler/metotlar, `_camelCase` private alanlar, `I` önekli arayüzler.
- Olay struct'ları geçmiş zaman: `BalanceChanged`, `LevelUnlocked`.

## Testler
- Her yeni kural için test; hata yolları dahil (provider exception, disk hatası, saat geri alma...).
- NUnit 3 klasik API (`Assert.AreEqual`, `Assert.That`) — Unity Test Framework uyumu. `async Task` test yok; `AsyncTest.Run` kullan.

## Git
- Ana dal `main`; özellik dalları `feat/...`, `fix/...`. Conventional Commits.
