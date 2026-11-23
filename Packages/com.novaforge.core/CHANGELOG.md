# Changelog
All notable changes to `com.novaforge.core` are documented here. Format: [Keep a Changelog](https://keepachangelog.com), versioning: SemVer (see docs/VERSIONING.md).

## [Unreleased]
### Added
- `NovaForge.Ads.AdMob` adapter (Google Mobile Ads Unity plugin 11.x): UMP consent flow, rewarded/interstitial/banner,
  impression revenue event, test-vs-production ad unit guard. Compiles only when `com.google.ads.mobile` >= 11.0.0 is installed.
  Not yet compiled in Unity.
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
