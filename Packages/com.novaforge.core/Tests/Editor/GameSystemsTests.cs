using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NovaForge.Core.Analytics;
using NovaForge.Core.Config;
using NovaForge.Core.Economy;
using NovaForge.Core.Localization;
using NovaForge.Core.Meta;
using NovaForge.Core.Progression;
using NUnit.Framework;

namespace NovaForge.Core.Tests
{
    public class ProgressionTests
    {
        private static ListLevelCatalog Catalog(int n)
        {
            var ids = new List<string>();
            for (int i = 1; i <= n; i++) ids.Add("level_" + i.ToString("000"));
            return new ListLevelCatalog(ids);
        }

        [Test]
        public void CompletingUnlocksNext_AndPersists()
        {
            var env = new TestEnv();
            var p = new ProgressionService(Catalog(3), env.Saves, env.Events);
            Assert.IsTrue(p.IsUnlocked(0));
            Assert.IsFalse(p.IsUnlocked(1));
            var r = p.RecordCompletion("level_001", 2, 500);
            Assert.IsTrue(r.FirstCompletion);
            Assert.AreEqual(1, r.NewlyUnlockedIndex);
            env.Restart();
            var p2 = new ProgressionService(Catalog(3), env.Saves, env.Events);
            Assert.IsTrue(p2.IsUnlocked(1));
            Assert.AreEqual(2, p2.TotalStars);
            Assert.AreEqual(1, p2.NextLevelIndex);
        }

        [Test]
        public void Replay_KeepsBest_AndReportsStarsGained()
        {
            var env = new TestEnv();
            var p = new ProgressionService(Catalog(3), env.Saves, env.Events);
            p.RecordCompletion("level_001", 3, 900);
            var r = p.RecordCompletion("level_001", 1, 100);
            Assert.IsFalse(r.FirstCompletion);
            Assert.AreEqual(0, r.StarsGained);
            Assert.AreEqual(3, p.Get("level_001").BestStars);
            Assert.AreEqual(900, p.Get("level_001").BestScore);
            Assert.AreEqual(-1, r.NewlyUnlockedIndex);
        }

        [Test]
        public void LockedLevel_CannotBeCompleted()
        {
            var env = new TestEnv();
            var p = new ProgressionService(Catalog(3), env.Saves, env.Events);
            Assert.Throws<InvalidOperationException>(() => p.RecordCompletion("level_003", 3, 1));
        }

        [Test]
        public void StarsClamped()
        {
            var env = new TestEnv();
            var p = new ProgressionService(Catalog(2), env.Saves, env.Events);
            Assert.AreEqual(3, p.RecordCompletion("level_001", 99, 1).Stars);
        }

        [Test]
        public void Catalog_RejectsDuplicates()
        {
            Assert.Throws<ArgumentException>(() => new ListLevelCatalog(new[] { "a", "a" }));
        }

        [Test]
        public void ScoreAndObjectives()
        {
            var s = new ScoreTracker(100, 200, 300);
            s.Add(250);
            Assert.AreEqual(2, s.Stars);
            var o = new ObjectiveTracker().Add(new Objective("purple", 2)).Add(new Objective("green", 1));
            bool done = false;
            o.AllCompleted += () => done = true;
            o.Report("purple"); o.Report("green");
            Assert.IsFalse(done);
            o.Report("purple");
            Assert.IsTrue(done);
        }

        [Test]
        public void Difficulty_EasesAfterFails_WithinBounds()
        {
            var env = new TestEnv();
            var c = new ConfigurationManager(new ConfigSchema().AddCoreEntries(), env.Log);
            var d = new DifficultyService(c);
            Assert.AreEqual(1.0f, d.GetMultiplier(0), 1e-5);
            Assert.AreEqual(0.9f, d.GetMultiplier(3), 1e-5);
            Assert.AreEqual(0.7f, d.GetMultiplier(100), 1e-5);
            c.ApplyOverrides(new Dictionary<string, string> { { CoreConfig.DifficultyAdaptiveEnabled, "false" } }, "test");
            Assert.AreEqual(1.0f, d.GetMultiplier(100), 1e-5);
        }
    }

    public class LevelSessionTests
    {
        private TestEnv _env;
        private EconomyService _economy;
        private PlayerStatistics _stats;
        private ProgressionService _progress;
        private LevelSessionService _session;

        [SetUp]
        public void SetUp()
        {
            _env = new TestEnv();
            _economy = _env.Economy();
            _stats = new PlayerStatistics(_env.Saves, _env.Events);
            _progress = new ProgressionService(new ListLevelCatalog(new[] { "l1", "l2", "l3" }), _env.Saves, _env.Events);
            _session = new LevelSessionService(_progress, _stats, new RewardService(_economy, _env.Events),
                new StarBasedRewardPolicy("coin", perStar: 10, firstClearBonus: 20), _env.Analytics(_env.Consent()), null, _env.Clock, _env.Events);
        }

        [Test]
        public void Win_GrantsFirstClearOnce_UpdatesStatsAndAnalytics()
        {
            _session.Start("l1");
            var end = _session.Win(3, 1000);
            Assert.AreEqual(150, _economy.GetBalance("coin"));
            Assert.IsNotNull(end.RewardGranted);
            Assert.AreEqual(1, _stats.Get(CoreStats.LevelsCompleted));
            Assert.AreEqual(3, _stats.Get(CoreStats.StarsEarned));
            Assert.AreEqual(1, _stats.Get(CoreStats.ThreeStarLevels));

            _session.Start("l1");
            _session.Win(3, 1000);
            Assert.AreEqual(150, _economy.GetBalance("coin"), "replay with no new stars gives nothing (replayPerStar=0)");
            Assert.AreEqual(1, _env.AnalyticsProvider.Count(AnalyticsEvents.LevelStarted) - 1);
        }

        [Test]
        public void Lose_CountsConsecutiveFails_ResetOnWin()
        {
            _session.Start("l1"); _session.Lose("out_of_moves");
            _session.Start("l1"); _session.Lose("out_of_moves");
            Assert.AreEqual(2, _session.ConsecutiveFails);
            _session.Start("l1"); _session.Win(1, 10);
            Assert.AreEqual(0, _session.ConsecutiveFails);
            Assert.AreEqual(2, _progress.Get("l1").Fails);
        }

        [Test]
        public void CannotStartLocked_OrWinTwice()
        {
            Assert.Throws<InvalidOperationException>(() => _session.Start("l3"));
            _session.Start("l1");
            _session.Win(1, 1);
            Assert.Throws<InvalidOperationException>(() => _session.Win(1, 1));
        }

        [Test]
        public void Restart_LogsEvent_AndStartsFresh()
        {
            _session.Start("l1");
            _session.Restart();
            Assert.AreEqual(LevelSessionState.Playing, _session.State);
            Assert.AreEqual(1, _env.AnalyticsProvider.Count(AnalyticsEvents.LevelRestarted));
            Assert.AreEqual(2, _progress.Get("l1").Attempts);
        }
    }

    public class DailyRewardTests
    {
        private TestEnv _env;
        private EconomyService _economy;

        private DailyRewardService Create(DailyRewardOptions o = null)
        {
            var schedule = DailyRewardService.ParseSchedule(JToken.Parse("[{\"coin\":10},{\"coin\":20},{\"coin\":30,\"hint\":1}]"));
            return new DailyRewardService(() => schedule, () => true, new RewardService(_economy, _env.Events),
                _env.Analytics(_env.Consent()), _env.Saves, _env.Clock, _env.Log, o);
        }

        [SetUp]
        public void SetUp()
        {
            _env = new TestEnv();
            _economy = _env.Economy();
        }

        [Test]
        public void ClaimOncePerDay_StreakAdvances_Cycles()
        {
            var d = Create();
            Assert.AreEqual(DailyClaimStatus.Claimed, d.Claim());
            Assert.AreEqual(DailyClaimStatus.AlreadyClaimedToday, d.Claim());
            Assert.AreEqual(110, _economy.GetBalance("coin"));
            Assert.Greater(d.TimeUntilNextClaim, TimeSpan.Zero);
            for (int i = 0; i < 3; i++) { _env.Clock.Advance(TimeSpan.FromDays(1)); d.Claim(); }
            // day1 10, day2 20, day3 30+hint, day4 cycles to 10
            Assert.AreEqual(100 + 10 + 20 + 30 + 10, _economy.GetBalance("coin"));
            Assert.AreEqual(1, _economy.GetBalance("hint"));
        }

        [Test]
        public void MissedDay_ResetsStreak()
        {
            var d = Create();
            d.Claim();
            _env.Clock.Advance(TimeSpan.FromDays(1));
            d.Claim();
            _env.Clock.Advance(TimeSpan.FromDays(3));
            Assert.AreEqual(0, d.CurrentDayIndex);
        }

        [Test]
        public void ClockRollback_Blocked()
        {
            var d = Create();
            d.Claim();
            _env.Clock.Advance(TimeSpan.FromDays(-3));
            Assert.AreEqual(DailyClaimStatus.ClockTampering, d.Claim());
            Assert.IsFalse(d.CanClaim);
        }

        [Test]
        public void SurvivesRestart()
        {
            Create().Claim();
            _env.Restart();
            _economy = _env.Economy();
            Assert.AreEqual(DailyClaimStatus.AlreadyClaimedToday, Create().Claim());
        }

        [Test]
        public void InvalidSchedule_Throws()
        {
            Assert.Throws<FormatException>(() => DailyRewardService.ParseSchedule(JToken.Parse("[]")));
            Assert.Throws<FormatException>(() => DailyRewardService.ParseSchedule(JToken.Parse("{}")));
        }

        [Test]
        public void CoreDefaultSchedule_Parses()
        {
            var c = new ConfigurationManager(new ConfigSchema().AddCoreEntries(), _env.Log);
            Assert.AreEqual(7, DailyRewardService.ParseSchedule(c.GetJson(CoreConfig.DailyRewardsSchedule)).Count);
        }
    }

    public class MissionAndAchievementTests
    {
        [Test]
        public void DailyMission_ProgressClaimAndReset()
        {
            var env = new TestEnv();
            var economy = env.Economy();
            var stats = new PlayerStatistics(env.Saves, env.Events);
            stats.Increment(CoreStats.LevelsCompleted, 10); // history before mission period must not count
            var missions = new MissionService(new[]
            {
                new MissionDefinition("win_3", CoreStats.LevelsCompleted, 3, RewardBundle.Of("coin", 25), MissionPeriod.Daily),
            }, stats, new RewardService(economy, env.Events), env.Analytics(env.Consent()), env.Saves, env.Clock);

            Assert.AreEqual(0, missions.GetAll()[0].Current);
            Assert.AreEqual(MissionClaimStatus.NotComplete, missions.Claim("win_3"));
            stats.Increment(CoreStats.LevelsCompleted, 3);
            Assert.AreEqual(MissionClaimStatus.Claimed, missions.Claim("win_3"));
            Assert.AreEqual(MissionClaimStatus.AlreadyClaimed, missions.Claim("win_3"));
            Assert.AreEqual(125, economy.GetBalance("coin"));

            env.Clock.Advance(TimeSpan.FromDays(1));
            Assert.AreEqual(0, missions.GetAll()[0].Current);
            Assert.IsFalse(missions.GetAll()[0].Claimed);
        }

        [Test]
        public void Achievement_UnlocksOnStat_GrantsOnce()
        {
            var env = new TestEnv();
            var economy = env.Economy();
            var stats = new PlayerStatistics(env.Saves, env.Events);
            var unlocked = new List<string>();
            env.Events.Subscribe<AchievementUnlocked>(a => unlocked.Add(a.Achievement.Id));
            var ach = new AchievementService(new[] { new AchievementDefinition("stars_10", CoreStats.StarsEarned, 10, RewardBundle.Of("gem", 5)) },
                stats, new RewardService(economy, env.Events), env.Analytics(env.Consent()), env.Saves, env.Events);

            stats.Increment(CoreStats.StarsEarned, 6);
            Assert.AreEqual(0.6f, ach.GetProgress("stars_10"), 1e-5);
            stats.Increment(CoreStats.StarsEarned, 6);
            stats.Increment(CoreStats.StarsEarned, 6);
            Assert.IsTrue(ach.IsUnlocked("stars_10"));
            CollectionAssert.AreEqual(new[] { "stars_10" }, unlocked);
            Assert.AreEqual(5, economy.GetBalance("gem"));
        }
    }

    public class TutorialTests
    {
        [Test]
        public void Steps_Advance_Complete_NotRepeated()
        {
            var env = new TestEnv();
            var t = new TutorialService(new[] { new TutorialDefinition("intro", new[] { "swipe", "merge", "goal" }) }, env.Analytics(env.Consent()), env.Saves, env.Events);
            Assert.IsTrue(t.TryStart("intro"));
            Assert.IsFalse(t.Advance("merge"), "stale callback ignored");
            Assert.IsTrue(t.Advance("swipe"));
            Assert.IsTrue(t.Advance("merge"));
            Assert.IsTrue(t.Advance("goal"));
            Assert.IsTrue(t.IsCompleted("intro"));
            Assert.IsNull(t.ActiveTutorialId);
            env.Restart();
            var t2 = new TutorialService(new[] { new TutorialDefinition("intro", new[] { "swipe" }) }, env.Analytics(env.Consent()), env.Saves, env.Events);
            Assert.IsFalse(t2.TryStart("intro"));
        }

        [Test]
        public void Skip_RespectsSkippable()
        {
            var env = new TestEnv();
            var t = new TutorialService(new[] { new TutorialDefinition("forced", new[] { "a" }, skippable: false) }, env.Analytics(env.Consent()), env.Saves, env.Events);
            t.TryStart("forced");
            Assert.IsFalse(t.Skip());
        }
    }

    public class LocalizationTests
    {
        private static LocalizationService Create(TestEnv env)
        {
            var l = new LocalizationService(env.Log, env.Events);
            l.AddTableJson("en", "{\"play\":\"Play\",\"coins\":\"{0} coins\",\"only_en\":\"English only\"}");
            l.AddTableJson("tr", "{\"play\":\"Oyna\",\"coins\":\"{0} altın\"}");
            l.AddTableJson("de", "{\"play\":\"Spielen\",\"coins\":\"Münzen\"}");
            return l;
        }

        [Test]
        public void Fallback_Chain()
        {
            var env = new TestEnv();
            var l = Create(env);
            l.SetLanguage("tr");
            Assert.AreEqual("Oyna", l.Get("play"));
            Assert.AreEqual("English only", l.Get("only_en"));
            Assert.AreEqual("#missing#", l.Get("missing"));
            Assert.AreEqual("1.500 altın", l.Format("coins", 1500.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("tr"))));
        }

        [Test]
        public void UnknownLanguage_FallsBackToEnglish()
        {
            var env = new TestEnv();
            var l = Create(env);
            l.SetLanguage("ko");
            Assert.AreEqual("en", l.Language);
        }

        [Test]
        public void CultureResolution()
        {
            Assert.AreEqual("pt", Languages.Resolve("pt-BR"));
            Assert.AreEqual("tr", Languages.Resolve("tr_TR"));
            Assert.AreEqual("en", Languages.Resolve("zh-CN"));
            Assert.AreEqual("en", Languages.Resolve(null));
        }

        [Test]
        public void Validation_FindsMissingAndPlaceholderMismatch()
        {
            var env = new TestEnv();
            var l = Create(env);
            CollectionAssert.AreEqual(new[] { "only_en" }, l.FindMissingKeys("tr"));
            CollectionAssert.AreEqual(new[] { "coins" }, l.FindPlaceholderMismatches("de"));
        }

        [Test]
        public void NonStringValue_Rejected()
        {
            var l = new LocalizationService(new TestEnv().Log);
            Assert.Throws<FormatException>(() => l.AddTableJson("en", "{\"a\":1}"));
        }
    }
}
