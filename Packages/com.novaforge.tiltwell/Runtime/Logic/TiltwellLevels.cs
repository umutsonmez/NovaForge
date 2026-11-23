using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NovaForge.Tiltwell
{
    /// <summary>One shipped level. Par = optimal move count proven by the solver at build time.</summary>
    public sealed class LevelData
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("rows")] public string[] Rows;
        [JsonProperty("par")] public int Par;
        [JsonProperty("tier")] public int Tier;

        /// <summary>Moves allowed before the attempt fails. Generous: the goal is thinking, not move counting.</summary>
        [JsonIgnore] public int MoveLimit => Par * 2 + 4;

        /// <summary>3 stars at par, 2 within par+3, otherwise 1.</summary>
        public int StarsFor(int moves) => moves <= Par ? 3 : moves <= Par + 3 ? 2 : 1;
    }

    public sealed class LevelPack
    {
        [JsonProperty("version")] public int Version = 1;
        [JsonProperty("levels")] public List<LevelData> Levels = new List<LevelData>();

        public static LevelPack FromJson(string json) =>
            JsonConvert.DeserializeObject<LevelPack>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None })
            ?? throw new FormatException("Empty level pack.");

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }

    /// <summary>Generation settings for one difficulty tier.</summary>
    public sealed class TierSpec
    {
        public int Tier;
        public int Count;
        public int MinSize, MaxSize;
        /// <summary>Wells per level.</summary>
        public int MinWells, MaxWells;
        /// <summary>How many wells need a mixed (secondary) color.</summary>
        public int MinMixedWells, MaxMixedWells;
        /// <summary>Droplets that are not needed for any well (they get in the way).</summary>
        public int MaxDistractors;
        public int MaxHoles;
        public float WallDensity;
        public int MinPar, MaxPar;
    }

    /// <summary>
    /// Seeded random generator + solver filter. Every accepted level is solvable, its par is the proven optimum, and it
    /// falls inside the tier's par range. Same seed → same levels (reproducible builds).
    /// </summary>
    public static class LevelGenerator
    {
        private static readonly Paint[] Primaries = { Paint.Red, Paint.Yellow, Paint.Blue };
        private static readonly Paint[] Mixed = { Paint.Orange, Paint.Purple, Paint.Green };

        public static readonly TierSpec[] DefaultTiers =
        {
            new TierSpec { Tier = 1, Count = 5, MinSize = 5, MaxSize = 6, MinWells = 1, MaxWells = 2, MinMixedWells = 0, MaxMixedWells = 0, MaxDistractors = 0, MaxHoles = 0, WallDensity = 0.10f, MinPar = 2, MaxPar = 4 },
            new TierSpec { Tier = 2, Count = 7, MinSize = 6, MaxSize = 6, MinWells = 1, MaxWells = 1, MinMixedWells = 1, MaxMixedWells = 1, MaxDistractors = 0, MaxHoles = 0, WallDensity = 0.12f, MinPar = 3, MaxPar = 5 },
            new TierSpec { Tier = 3, Count = 8, MinSize = 6, MaxSize = 7, MinWells = 2, MaxWells = 2, MinMixedWells = 1, MaxMixedWells = 1, MaxDistractors = 1, MaxHoles = 1, WallDensity = 0.14f, MinPar = 4, MaxPar = 7 },
            new TierSpec { Tier = 4, Count = 8, MinSize = 7, MaxSize = 7, MinWells = 2, MaxWells = 3, MinMixedWells = 1, MaxMixedWells = 2, MaxDistractors = 1, MaxHoles = 2, WallDensity = 0.15f, MinPar = 6, MaxPar = 9 },
            new TierSpec { Tier = 5, Count = 8, MinSize = 7, MaxSize = 8, MinWells = 3, MaxWells = 3, MinMixedWells = 2, MaxMixedWells = 3, MaxDistractors = 1, MaxHoles = 2, WallDensity = 0.16f, MinPar = 8, MaxPar = 12 },
        };

        public static LevelPack Generate(int seed, IReadOnlyList<TierSpec> tiers = null, int maxAttemptsPerLevel = 20_000)
        {
            tiers ??= DefaultTiers;
            var rng = new Random(seed);
            var pack = new LevelPack();
            var seen = new HashSet<string>();
            int n = 0;
            foreach (var tier in tiers)
            {
                // Spread par evenly across the tier so difficulty ramps instead of clustering.
                for (int i = 0; i < tier.Count; i++)
                {
                    int targetPar = tier.MinPar + (tier.MaxPar - tier.MinPar) * i / Math.Max(1, tier.Count - 1);
                    LevelData level = null;
                    for (int attempt = 0; attempt < maxAttemptsPerLevel && level == null; attempt++)
                    {
                        // Accept the exact target par first; relax to the whole tier range after many attempts.
                        bool relaxed = attempt > maxAttemptsPerLevel / 2;
                        var rows = RandomRows(rng, tier);
                        if (rows == null) continue;
                        var key = string.Join("|", rows);
                        if (seen.Contains(key)) continue;
                        var (board, start) = Board.Parse(rows);
                        var sol = Solver.Solve(board, start, tier.MaxPar, 60_000);
                        if (!sol.Solved) continue;
                        int par = sol.Moves.Count;
                        bool ok = relaxed ? par >= tier.MinPar && par <= tier.MaxPar : par == targetPar;
                        if (!ok || !IsInteresting(board, start, sol.Moves)) continue;
                        seen.Add(key);
                        level = new LevelData { Id = $"l{++n:000}", Rows = rows, Par = par, Tier = tier.Tier };
                    }
                    if (level == null) throw new InvalidOperationException($"Could not generate level {n + 1} (tier {tier.Tier}).");
                    pack.Levels.Add(level);
                }
            }
            return pack;
        }

        /// <summary>Rejects levels where the first move already solves most of it or where a random walk solves it.</summary>
        private static bool IsInteresting(Board board, PuzzleState start, List<Tilt> solution)
        {
            var r = new Random(solution.Count * 7919 + board.Width);
            for (int trial = 0; trial < 30; trial++)
            {
                var s = start;
                for (int m = 0; m < solution.Count; m++)
                {
                    var res = Rules.Apply(board, s, (Tilt)r.Next(4));
                    s = res.State;
                    if (s.IsSolved) return false; // random play at par length solved it: too easy
                }
            }
            return true;
        }

        private static string[] RandomRows(Random rng, TierSpec t)
        {
            int w = rng.Next(t.MinSize, t.MaxSize + 1);
            int h = rng.Next(t.MinSize, t.MaxSize + 1);
            var g = new char[h][];
            for (int y = 0; y < h; y++)
            {
                g[y] = new char[w];
                for (int x = 0; x < w; x++)
                {
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    g[y][x] = edge || rng.NextDouble() < t.WallDensity ? '#' : '.';
                }
            }

            var free = new List<(int x, int y)>();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                    if (g[y][x] == '.') free.Add((x, y));
            if (free.Count < 8) return null;

            (int x, int y) Take()
            {
                int i = rng.Next(free.Count);
                var c = free[i];
                free.RemoveAt(i);
                return c;
            }

            int wells = rng.Next(t.MinWells, t.MaxWells + 1);
            int mixed = Math.Min(wells, rng.Next(t.MinMixedWells, t.MaxMixedWells + 1));
            var dropColors = new List<Paint>();
            for (int i = 0; i < wells; i++)
            {
                Paint color;
                if (i < mixed)
                {
                    color = Mixed[rng.Next(Mixed.Length)];
                    foreach (var p in Primaries) if ((color & p) != 0) dropColors.Add(p); // needs two primaries
                }
                else
                {
                    color = Primaries[rng.Next(Primaries.Length)];
                    dropColors.Add(color);
                }
                var (x, y) = Take();
                g[y][x] = char.ToLowerInvariant(Board.Letter(color));
            }

            int distractors = t.MaxDistractors > 0 ? rng.Next(0, t.MaxDistractors + 1) : 0;
            for (int i = 0; i < distractors; i++) dropColors.Add(Primaries[rng.Next(Primaries.Length)]);
            foreach (var c in dropColors)
            {
                var (x, y) = Take();
                g[y][x] = Board.Letter(c);
            }

            int holes = t.MaxHoles > 0 ? rng.Next(0, t.MaxHoles + 1) : 0;
            for (int i = 0; i < holes && free.Count > 0; i++)
            {
                var (x, y) = Take();
                g[y][x] = 'x';
            }

            var rows = new string[h];
            for (int y = 0; y < h; y++) rows[y] = new string(g[y]);
            return rows;
        }
    }
}
