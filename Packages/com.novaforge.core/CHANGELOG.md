# Changelog
All notable changes to `com.novaforge.core` are documented here. Format: [Keep a Changelog](https://keepachangelog.com), versioning: SemVer (see docs/VERSIONING.md).

## [Unreleased]
### Added
- Ready-made views: `LevelResultView` (stars, reward, rewarded-ad x2, `LevelResultChosen` event), `DailyRewardView`
  (calendar, countdown, `EnqueueIfClaimable`), `ShopView` (soft + real money, restore on iOS), `UIButtonFeedback`, `RewardText`.
- Audio: `AudioService` (music crossfade, pooled SFX with cooldown and voice stealing, live settings, silent during
  full-screen ads), `AudioLibrary`, `Haptics` (Android amplitude one-shot, iOS heavy only).
- Core: `FullscreenAdStateChanged` event, `DailyRewardService.Schedule`, `ViewIds.LevelResult`, 15 more `CoreStrings` keys.
- Editor builders: level result, daily reward, shop, audio service.
- UI framework: engine-independent `NavigationService` (screen stack, popup stack, priority popup queue, back-button policy,
  serialized transitions), `PlayerSettingsService`, `CoreStrings` (8 languages), `IPrivacyOptionsProvider`.
- `NovaForge.UI` assembly (uGUI 2.0 + TMP): `UIView`, `UIManager`, `ConfirmPopup`, `SettingsView`, `LocalizedText`, `CurrencyDisplay`.
- `NovaForge.Editor`: NOVAFORGE ▸ UI menu (UI root, screen, popup, confirm popup, settings popup builders).
- PlayMode tests (`NovaForge.UI.Tests`).
- Config keys `privacy.policy_url`, `privacy.terms_url`, `support.email`; release validation requires an https privacy policy URL.
- `AdMobAdsProvider` implements `IPrivacyOptionsProvider`.
- `NovaForge.Ads.AdMob` adapter (Google Mobile Ads Unity plugin 11.x): UMP consent flow, rewarded/interstitial/banner,
  impression revenue event, test-vs-production ad unit guard. Compiles only when `com.google.ads.mobile` >= 11.0.0 is installed.
  Verified to compile with 11.7.0 in Unity 6000.3.29f1; SDK callbacks marshalled via `MobileAdsEventExecutor.ExecuteInUpdate`.
- `Projects/Sandbox`: development Unity project referencing the package; 103 EditMode tests pass in Unity Test Framework 1.6.0.
### Changed
- Newtonsoft dependency `com.unity.nuget.newtonsoft-json` 3.2.1 -> 3.2.2 (version bundled with Unity 6.3).
- `AnalyticsEvents.AdImpression`, `AnalyticsParams.AdFormat`.

## [0.1.0] - 2026-11-23
### Added
- Engine-independent core assembly (`NovaForge.Core`, `noEngineReferences: true`):
  ServiceRegistry (DI), EventBus, Logging, ErrorHandler, StateMachine, GameLifecycle, ObjectPool, Clock.
- Config: typed schema with ranges, layered ConfigurationManager, RemoteConfigService (timeout, validation, offline cache).
- Data: SaveManager (versioned envelope, SHA-256 integrity, migrations, backup recovery), FileSaveStorage (atomic writes), cloud save abstraction.
- Economy: resources (currencies + items), atomic idempotent transactions, daily earn caps, history; RewardService; EntitlementService.
- Ads: provider-independent AdsService with central frequency policy, reward-once guarantee, no-ads entitlement, consent; MockAdsProvider (tests only).
- Purchasing: PurchaseService (validate → fulfill once → finish), ServerPurchaseValidator, restore, deferred payments; MockPurchaseProvider (tests only).
- Analytics (consent-gated, sanitized), SessionTracker; ConsentService (GDPR/KVKK/ATT/COPPA flags).
- Progression, statistics, level sessions, missions, achievements, daily rewards, tutorials, difficulty, score/objectives.
- Localization (8 languages, fallback, validation helpers); ShopService.
- 98 EditMode tests (run under Unity Test Framework or `dotnet test`).
