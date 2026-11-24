# Changelog

## [Unreleased]
### Added
- `LevelMapView` + `LevelMapNode`: winding road level map, one colored world per tier, gold progress path, current-level marker; replaces `LevelSelectView`.
- New main menu (chips, logo, decorative mini board, nav buttons, daily reward button) and gameplay HUD (chips, badge, message bubble).
- `tiltwell.world_n` string in 8 languages.
- Tiers 6-10: 56 new levels (92 total). Brown wells (need red, yellow and blue) from tier 7, with a `tiltwell.tutorial_brown` hint on the first one. The first 36 levels are unchanged.
- Level map colors for worlds 6-10.
- Color teaching:
  - `ColorGuideView` popup (`color_guide`), shown once on the first mixing level and again when brown first appears.
  - Recipe bar on the gameplay screen up to tier 4 (and the brown world); tapping it opens the guide.
  - Free direction arrow on level 1 and on the first mixing level.
  - New types: `RecipeRow`, `ColorRecipes`.
- Sound: `TiltwellSoundBuilder` synthesizes 10 effects and a seamless music loop into `Runtime/Audio`, plus the `AudioLibrary`. Gameplay plays slide, bump, merge, deliver and fall sounds. `BoardView.Animate` takes a `landed` callback. Haptics fire on landing.

### Changed
- `BoardView`: themed board (panel, raised walls, deep holes, glowing wells, glossy droplets, squash while sliding, particles on merge/deliver). Sprites come from the theme.
- Scene builder uses a Tiltwell theme (sky to lilac background).

## [0.1.0] - 2026-11-23
### Added
- Rules engine (tilt, merge, color mixing, holes, wells), BFS solver, seeded level generator, `PuzzleSession` (undo, restart, hint, extra moves).
- 36 solver-verified levels in 5 tiers.
- Unity layer: `TiltwellApp`, `GameplayView`, `MainMenuView`, `LevelSelectView`, `BoardView`, `SwipeInput`, 8-language strings.
- Editor: NOVAFORGE ▸ Tiltwell ▸ Build Game Scene.
- Tests: 22 logic tests, end-to-end PlayMode tests.
