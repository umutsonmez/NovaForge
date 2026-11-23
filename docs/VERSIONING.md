# Sürümleme

- `com.novaforge.core` **SemVer** kullanır: `MAJOR.MINOR.PATCH`.
  - MAJOR: public API kırılması veya save formatında migration'sız değişiklik.
  - MINOR: geriye uyumlu yeni özellik.
  - PATCH: hata düzeltmesi.
- 1.0.0'a kadar (0.x) MINOR artışları kırıcı değişiklik içerebilir; CHANGELOG'da `### Breaking` başlığıyla belirtilir.
- Her sürüm: `package.json` versiyonu + `CHANGELOG.md` + git tag `core-vX.Y.Z`.
- Oyunlar paketi tag'e sabitler (`#core-v0.1.0`), otomatik en son sürümü çekmez.

## Save şeması
- Her slotun kendi `SaveVersion` sabiti vardır. Kalıcı veri şekli değişince versiyon +1 ve `ISaveMigration` eklenir.
- Migration'lar asla silinmez (eski sürümden gelen oyuncular için).
- Yeni sürümün kaydı eski uygulamada açılmaz (yerine backup/varsayılan); bu durum loglanır.

## Remote config
- Yeni anahtar = şemaya beyan + güvenli varsayılan. Eski uygulamalar bilmedikleri anahtarları yok sayar.
