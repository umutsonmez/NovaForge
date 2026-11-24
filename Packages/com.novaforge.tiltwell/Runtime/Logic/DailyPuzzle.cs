using System;

namespace NovaForge.Tiltwell
{
    /// <summary>
    /// One new puzzle per calendar day, the same for every player (seeded by the day number). Each weekday uses a
    /// different twist so the daily stays varied. Generated on the device: every puzzle is solver-checked like the
    /// shipped levels.
    /// </summary>
    public static class DailyPuzzle
    {
        private static readonly TierSpec[] Twists =
        {
            new TierSpec { Tier = 0, Count = 1, MinSize = 6, MaxSize = 7, MinWells = 2, MaxWells = 2, MinMixedWells = 1, MaxMixedWells = 2, MaxDistractors = 1, MaxHoles = 1, WallDensity = 0.14f, MinPar = 6, MaxPar = 9 },
            new TierSpec { Tier = 0, Count = 1, MinSize = 6, MaxSize = 7, MinWells = 2, MaxWells = 2, MinMixedWells = 1, MaxMixedWells = 1, MaxDistractors = 1, MaxHoles = 1, MaxSticky = 2, WallDensity = 0.13f, MinPar = 6, MaxPar = 9 },
            new TierSpec { Tier = 0, Count = 1, MinSize = 6, MaxSize = 7, MinWells = 1, MaxWells = 2, MinBrownWells = 1, MaxBrownWells = 1, MinMixedWells = 0, MaxMixedWells = 1, MaxDistractors = 0, MaxHoles = 1, WallDensity = 0.13f, MinPar = 6, MaxPar = 10 },
            new TierSpec { Tier = 0, Count = 1, MinSize = 6, MaxSize = 7, MinWells = 2, MaxWells = 2, MinMixedWells = 1, MaxMixedWells = 2, MaxDistractors = 1, MaxHoles = 1, MaxArrows = 2, WallDensity = 0.12f, MinPar = 6, MaxPar = 9 },
            new TierSpec { Tier = 0, Count = 1, MinSize = 6, MaxSize = 7, MinWells = 2, MaxWells = 2, MinMixedWells = 1, MaxMixedWells = 2, MaxDistractors = 0, MaxHoles = 1, MaxPads = 1, WallDensity = 0.13f, MinPar = 6, MaxPar = 9 },
        };

        /// <summary>Days since 2026-01-01 for a local calendar date (only the date part is used).</summary>
        public static int DayNumber(DateTime localDate) => (int)(localDate.Date - new DateTime(2026, 1, 1)).TotalDays;

        public static string IdFor(int day) => "daily_" + day;

        /// <summary>Deterministic puzzle for a day. Takes up to a few seconds: call it off the main thread.</summary>
        public static LevelData Generate(int day)
        {
            var spec = Twists[((day % Twists.Length) + Twists.Length) % Twists.Length];
            var pack = LevelGenerator.Generate(unchecked(day * 7919 + 20261124), new[] { spec }, 6_000);
            var level = pack.Levels[0];
            level.Id = IdFor(day);
            return level;
        }

        /// <summary>Coins for finishing today's puzzle at a given streak (grows for a week, then stays).</summary>
        public static int Reward(int streak) => 30 + 10 * Math.Min(Math.Max(streak, 1), 7);
    }

    /// <summary>Daily streak bookkeeping (saved by the game).</summary>
    public sealed class DailyPuzzleState
    {
        /// <summary>Day number of the last finished daily, or int.MinValue if never.</summary>
        public int LastDay = int.MinValue;
        public int Streak;
        public int BestStreak;
        public int TotalSolved;

        public bool IsDone(int day) => LastDay == day;

        /// <summary>Streak the player would have if they finished <paramref name="day"/> now.</summary>
        public int StreakIfSolved(int day) => LastDay == day ? Streak : LastDay == day - 1 ? Streak + 1 : 1;

        /// <summary>Streak shown on the menu: broken if yesterday was missed.</summary>
        public int CurrentStreak(int today) => LastDay == today || LastDay == today - 1 ? Streak : 0;

        /// <returns>false if this day was already completed (no second reward).</returns>
        public bool Complete(int day)
        {
            if (LastDay == day) return false;
            Streak = StreakIfSolved(day);
            LastDay = day;
            BestStreak = Math.Max(BestStreak, Streak);
            TotalSolved++;
            return true;
        }
    }
}
