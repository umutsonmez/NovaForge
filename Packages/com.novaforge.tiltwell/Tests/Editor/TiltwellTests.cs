using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace NovaForge.Tiltwell.Tests
{
    public class RulesTests
    {
        private static (Board, PuzzleState) P(params string[] rows) => Board.Parse(rows);

        private static PuzzleState Tilt(Board b, PuzzleState s, params Tilt[] tilts)
        {
            foreach (var t in tilts) s = Rules.Apply(b, s, t).State;
            return s;
        }

        [Test]
        public void Droplet_SlidesUntilWall_AndStopsOnMatchingWell()
        {
            var (b, s) = P(
                "#####",
                "#.y##",
                "#Y..#",
                "#...#",
                "#####");
            s = Tilt(b, s, NovaForge.Tiltwell.Tilt.Up);
            Assert.AreEqual(b.Pos(1, 1), s.Droplets[0].Pos);
            var r = Rules.Apply(b, s, NovaForge.Tiltwell.Tilt.Right);
            Assert.IsTrue(r.State.IsSolved);
            Assert.AreEqual(0, r.State.Droplets.Count);
            Assert.IsTrue(r.Steps.Any(x => x.Fate == DropletFate.Delivered));
        }

        [Test]
        public void PassingOverWell_DoesNotDeliver()
        {
            var (b, s) = P(
                "######",
                "#R.r.#",
                "######");
            var r = Rules.Apply(b, s, NovaForge.Tiltwell.Tilt.Right);
            Assert.IsFalse(r.State.IsSolved);
            Assert.AreEqual(b.Pos(4, 1), r.State.Droplets[0].Pos);
        }

        [Test]
        public void WrongColor_OnWell_IsNotDelivered()
        {
            var (b, s) = P(
                "#####",
                "#Y.r#",
                "#####");
            var r = Rules.Apply(b, s, NovaForge.Tiltwell.Tilt.Right);
            Assert.IsFalse(r.State.IsSolved);
            Assert.AreEqual(1, r.State.Droplets.Count);
        }

        [Test]
        public void Collision_MixesColors_FrontDropletStaysInPlace()
        {
            var (b, s) = P(
                "######",
                "#R.B.#",
                "#...g#",
                "######");
            var r = Rules.Apply(b, s, NovaForge.Tiltwell.Tilt.Right);
            Assert.AreEqual(1, r.State.Droplets.Count);
            Assert.AreEqual(Paint.Purple, r.State.Droplets[0].Color);
            Assert.AreEqual(b.Pos(4, 1), r.State.Droplets[0].Pos);
            var merge = r.Steps.Single(x => x.Fate == DropletFate.Merged);
            Assert.AreEqual(b.Pos(1, 1), merge.From);
        }

        [Test]
        public void SameColors_MergeWithoutChangingColor()
        {
            var (b, s) = P(
                "#####",
                "#RR.#",
                "#..r#",
                "#####");
            var after = Tilt(b, s, NovaForge.Tiltwell.Tilt.Right);
            Assert.AreEqual(1, after.Droplets.Count);
            Assert.AreEqual(Paint.Red, after.Droplets[0].Color);
        }

        [Test]
        public void Hole_SwallowsDroplet_AndStuckIsDetected()
        {
            var (b, s) = P(
                "#####",
                "#Rx.#",
                "#..r#",
                "#####");
            var r = Rules.Apply(b, s, NovaForge.Tiltwell.Tilt.Right);
            Assert.AreEqual(0, r.State.Droplets.Count);
            Assert.IsTrue(r.Steps.Single().Fate == DropletFate.FellInHole);
            Assert.IsFalse(Rules.CanStillSolve(b, r.State));
        }

        [Test]
        public void NoOpTilt_DoesNotChangeState()
        {
            var (b, s) = P(
                "#####",
                "#R..#",
                "#..r#",
                "#####");
            var r = Rules.Apply(b, s, NovaForge.Tiltwell.Tilt.Left);
            Assert.IsFalse(r.Changed);
            Assert.AreSame(s, r.State);
        }

        [Test]
        public void DropletIds_AreStableAcrossMoves()
        {
            var (b, s) = P(
                "######",
                "#R..B#",
                "#....#",
                "#..g.#",
                "######");
            var ids = s.Droplets.Select(d => d.Id).OrderBy(i => i).ToArray();
            var after = Tilt(b, s, NovaForge.Tiltwell.Tilt.Down);
            CollectionAssert.AreEquivalent(ids, after.Droplets.Select(d => d.Id).ToArray());
        }

        [Test]
        public void Parse_RejectsBadLevels()
        {
            Assert.Throws<FormatException>(() => Board.Parse(new[] { "#####", "#R..#", "#####" }), "no wells");
            Assert.Throws<FormatException>(() => Board.Parse(new[] { "#####", "#R.r.", "#####" }), "open border");
            Assert.Throws<FormatException>(() => Board.Parse(new[] { "#####", "#R.r#", "####" }), "ragged");
            Assert.Throws<FormatException>(() => Board.Parse(new[] { "#####", "#R?r#", "#####" }), "unknown char");
        }

        [Test]
        public void Solver_FindsShortestSolution_AndReplays()
        {
            var (b, s) = P(
                "######",
                "#R.B.#",
                "#...p#",
                "######");
            var sol = Solver.Solve(b, s);
            Assert.IsTrue(sol.Solved);
            Assert.AreEqual(2, sol.Moves.Count); // right (mix to purple at the wall), down (onto the well)
            Assert.IsTrue(Tilt(b, s, sol.Moves.ToArray()).IsSolved);
        }

        [Test]
        public void Solver_ReportsUnsolvable()
        {
            var (b, s) = P(
                "#####",
                "#R.y#",
                "#####");
            var sol = Solver.Solve(b, s);
            Assert.IsFalse(sol.Solved);
            Assert.IsFalse(sol.HitLimit, "proven unsolvable, not just out of budget");
        }

        [Test]
        public void Stars_FollowPar()
        {
            var l = new LevelData { Par = 5 };
            Assert.AreEqual(3, l.StarsFor(5));
            Assert.AreEqual(2, l.StarsFor(8));
            Assert.AreEqual(1, l.StarsFor(9));
            Assert.AreEqual(14, l.MoveLimit);
        }

        [Test]
        public void Generator_IsDeterministic()
        {
            var tiers = new[] { LevelGenerator.DefaultTiers[0] };
            Assert.AreEqual(LevelGenerator.Generate(42, tiers).ToJson(), LevelGenerator.Generate(42, tiers).ToJson());
        }
    }

    /// <summary>Validates the level pack that ships with the game.</summary>
    public class LevelPackTests
    {
        internal static LevelPack LoadShipped()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "levels.json"),                         // dotnet test
                Path.GetFullPath("Packages/com.novaforge.tiltwell/Runtime/Levels/levels.json"), // Unity editor
            };
            foreach (var p in candidates) if (File.Exists(p)) return LevelPack.FromJson(File.ReadAllText(p));
            Assert.Fail("levels.json not found: " + string.Join(", ", candidates));
            return null;
        }

        [Test]
        public void AtLeast30Levels_UniqueIds_UniqueLayouts()
        {
            var pack = LoadShipped();
            Assert.GreaterOrEqual(pack.Levels.Count, 30);
            Assert.AreEqual(pack.Levels.Count, pack.Levels.Select(l => l.Id).Distinct().Count());
            Assert.AreEqual(pack.Levels.Count, pack.Levels.Select(l => string.Join("|", l.Rows)).Distinct().Count());
        }

        [Test]
        public void EveryLevel_IsSolvable_InExactlyPar()
        {
            foreach (var level in LoadShipped().Levels)
            {
                var (board, start) = Board.Parse(level.Rows);
                var sol = Solver.Solve(board, start, level.Par + 1);
                Assert.IsTrue(sol.Solved, $"{level.Id} unsolvable");
                Assert.AreEqual(level.Par, sol.Moves.Count, $"{level.Id}: stored par {level.Par}, optimal {sol.Moves.Count}");
                var s = start;
                foreach (var t in sol.Moves) s = Rules.Apply(board, s, t).State;
                Assert.IsTrue(s.IsSolved, $"{level.Id}: replay failed");
            }
        }

        [Test]
        public void Difficulty_RampsByTier()
        {
            var levels = LoadShipped().Levels;
            for (int i = 1; i < levels.Count; i++)
                Assert.GreaterOrEqual(levels[i].Tier, levels[i - 1].Tier, $"{levels[i].Id} tier goes down");
            double AvgPar(int tier) => levels.Where(l => l.Tier == tier).Average(l => l.Par);
            var tiers = levels.Select(l => l.Tier).Distinct().OrderBy(t => t).ToList();
            for (int i = 1; i < tiers.Count; i++)
                Assert.Greater(AvgPar(tiers[i]), AvgPar(tiers[i - 1]), $"tier {tiers[i]} not harder than {tiers[i - 1]}");
        }

        [Test]
        public void MixingLevels_ActuallyRequireMixing()
        {
            foreach (var level in LoadShipped().Levels)
            {
                var (board, start) = Board.Parse(level.Rows);
                bool needsMix = board.WellColors.Any(c => c != Paint.Red && c != Paint.Yellow && c != Paint.Blue);
                if (!needsMix) continue;
                foreach (var w in board.WellColors.Where(c => c != Paint.Red && c != Paint.Yellow && c != Paint.Blue))
                    Assert.IsFalse(start.Droplets.Any(d => d.Color == w), $"{level.Id}: pre-mixed {w} droplet makes mixing optional");
            }
        }
    }
}

namespace NovaForge.Tiltwell.Tests
{
    public class PuzzleSessionTests
    {
        private static LevelData Level(int par, params string[] rows) => new LevelData { Id = "t", Rows = rows, Par = par };

        [Test]
        public void Solve_CountsMoves_IgnoresNoOps()
        {
            var s = new PuzzleSession(Level(2, "#####", "#.y##", "#Y..#", "#...#", "#####"));
            Assert.IsNull(s.Tilt(Tilt.Left), "no-op ignored");
            Assert.AreEqual(0, s.Moves);
            s.Tilt(Tilt.Up);
            s.Tilt(Tilt.Right);
            Assert.AreEqual(PuzzleStatus.Solved, s.Status);
            Assert.AreEqual(3, s.Stars);
            Assert.IsNull(s.Tilt(Tilt.Down), "input ignored after solving");
        }

        [Test]
        public void Undo_And_Restart_RestoreState()
        {
            var s = new PuzzleSession(Level(2, "#####", "#.y##", "#Y..#", "#...#", "#####"));
            s.Tilt(Tilt.Down);
            s.Tilt(Tilt.Right);
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(1, s.Moves);
            s.Restart();
            Assert.AreSame(s.Start, s.State);
            Assert.AreEqual(0, s.Moves);
            Assert.IsFalse(s.Undo());
        }

        [Test]
        public void Stuck_WhenNeededDropletFallsInHole_UndoRecovers()
        {
            var s = new PuzzleSession(Level(2, "#####", "#Rx.#", "#..r#", "#####"));
            s.Tilt(Tilt.Right);
            Assert.AreEqual(PuzzleStatus.Stuck, s.Status);
            Assert.IsNull(s.Hint());
            Assert.IsTrue(s.Undo());
            Assert.AreEqual(PuzzleStatus.Playing, s.Status);
            Assert.IsNotNull(s.Hint());
        }

        [Test]
        public void OutOfMoves_ExtraMovesExtendLimit_StarsStillCountAllMoves()
        {
            // Par 1 → limit 6. Bounce up/down without solving.
            var s = new PuzzleSession(Level(1, "######", "#R...#", "#....#", "#...r#", "######"));
            for (int i = 0; i < 6; i++) s.Tilt(i % 2 == 0 ? Tilt.Down : Tilt.Up);
            Assert.AreEqual(PuzzleStatus.OutOfMoves, s.Status);
            Assert.IsNull(s.Tilt(Tilt.Right));
            s.AddMoves(3);
            Assert.AreEqual(PuzzleStatus.Playing, s.Status);
            s.Tilt(Tilt.Down);
            s.Tilt(Tilt.Right);
            Assert.AreEqual(PuzzleStatus.Solved, s.Status);
            Assert.AreEqual(1, s.Stars, "8 moves on par 1");
        }

        [Test]
        public void Hint_LeadsToSolution_OnEveryShippedLevel()
        {
            foreach (var level in LevelPackTests.LoadShipped().Levels)
            {
                var s = new PuzzleSession(level);
                for (int i = 0; i < level.Par; i++)
                {
                    var hint = s.Hint();
                    Assert.IsTrue(hint.HasValue, level.Id);
                    s.Tilt(hint.Value);
                }
                Assert.AreEqual(PuzzleStatus.Solved, s.Status, level.Id);
                Assert.AreEqual(3, s.Stars, level.Id);
            }
        }
    }

    public class ColorRecipeTests
    {
        [Test]
        public void Ingredients_ArePrimariesOfTheMix()
        {
            CollectionAssert.AreEqual(new[] { Paint.Red, Paint.Yellow }, ColorRecipes.Ingredients(Paint.Orange));
            CollectionAssert.AreEqual(new[] { Paint.Red, Paint.Blue }, ColorRecipes.Ingredients(Paint.Purple));
            CollectionAssert.AreEqual(new[] { Paint.Yellow, Paint.Blue }, ColorRecipes.Ingredients(Paint.Green));
            CollectionAssert.AreEqual(new[] { Paint.Red, Paint.Yellow, Paint.Blue }, ColorRecipes.Ingredients(Paint.Brown));
            Assert.IsFalse(ColorRecipes.IsMixed(Paint.Red));
            Assert.IsTrue(ColorRecipes.IsMixed(Paint.Brown));
        }

        [Test]
        public void MixedWells_ListsDistinctMixedTargets_InTeachingOrder()
        {
            var level = new LevelData { Id = "t", Par = 1, Tier = 1, Rows = new[] { "#######", "#g.Rox#", "#Y.rkg#", "#######" } };
            CollectionAssert.AreEqual(new[] { Paint.Orange, Paint.Green, Paint.Brown }, ColorRecipes.MixedWells(level));
        }
    }

    public class SpecialFloorTests
    {
        private static Board B(out PuzzleState s, params string[] rows)
        {
            var (b, st) = Board.Parse(rows);
            s = st;
            return b;
        }

        [Test]
        public void Sticky_StopsDroplet_ThenItCanLeave()
        {
            var b = B(out var s, "#######", "#R.~.r#", "#######");
            var r = Rules.Apply(b, s, Tilt.Right);
            Assert.AreEqual(b.Pos(3, 1), r.State.Droplets[0].Pos);
            r = Rules.Apply(b, r.State, Tilt.Right);
            Assert.IsTrue(r.State.IsSolved);
        }

        [Test]
        public void Arrow_PassesInItsDirection_BlocksOtherwise()
        {
            var b = B(out var s, "######", "#R>.r#", "######");
            Assert.IsTrue(Rules.Apply(b, s, Tilt.Right).State.IsSolved);
            b = B(out s, "######", "#R<.r#", "######");
            Assert.IsFalse(Rules.Apply(b, s, Tilt.Right).Changed);
        }

        [Test]
        public void PaintPad_AddsItsColor()
        {
            var b = B(out var s, "######", "#R2.o#", "######");
            Assert.AreEqual(Paint.Yellow, b.PadColors);
            Assert.IsTrue(Rules.CanStillSolve(b, s), "pad can supply the missing yellow");
            var r = Rules.Apply(b, s, Tilt.Right);
            Assert.IsTrue(r.State.IsSolved);
            Assert.AreEqual(Paint.Orange, r.Steps[0].Color);
        }

        [Test]
        public void Solver_UsesPads()
        {
            var b = B(out var s, "######", "#R3..#", "#...p#", "######");
            var sol = Solver.Solve(b, s);
            CollectionAssert.AreEqual(new[] { Tilt.Right, Tilt.Down }, sol.Moves);
        }
    }
}
