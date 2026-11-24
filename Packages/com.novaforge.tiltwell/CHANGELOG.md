# Changelog

## [Unreleased]
### Added
- `LevelMapView` + `LevelMapNode`: winding road level map, one colored world per tier, gold progress path, current-level marker; replaces `LevelSelectView`.
- New main menu (chips, logo, decorative mini board, nav buttons, daily reward button) and gameplay HUD (chips, badge, message bubble).
- `tiltwell.world_n` string in 8 languages.

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
