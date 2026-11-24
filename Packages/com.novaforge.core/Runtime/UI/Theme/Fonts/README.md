# Fonts

| File | Source | License |
|---|---|---|
| `Nunito-Bold.ttf`, `Nunito-Black.ttf` | Static instances (wght 700 / 900) of Nunito variable font, github.com/google/fonts/ofl/nunito | SIL Open Font License 1.1 (`Nunito-OFL.txt`) |
| `MaterialIconsRound-Regular.otf` | github.com/google/material-design-icons/font | Apache License 2.0 (`MaterialIcons-LICENSE.txt`) |

Static instances were generated with fontTools `varLib.instancer` (renaming not required by the OFL since the
Reserved Font Name clause does not apply to Nunito). Icon codepoints: see `UIIcons.cs`.
Japanese and Korean glyphs are not covered; add a Noto Sans JP/KR fallback when shipping those languages.
