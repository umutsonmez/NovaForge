using System;
using System.Collections.Generic;
using System.Text;

namespace NovaForge.Tiltwell
{
    /// <summary>
    /// Paint colors as a bit set of primaries. Mixing = union: red + blue = purple, all three = brown.
    /// Same colors merging keep their color (red + red = red).
    /// </summary>
    [Flags]
    public enum Paint : byte
    {
        None = 0,
        Red = 1,
        Yellow = 2,
        Blue = 4,
        Orange = Red | Yellow,
        Purple = Red | Blue,
        Green = Yellow | Blue,
        Brown = Red | Yellow | Blue,
    }

    public enum Tilt { Up, Down, Left, Right }

    public enum Cell : byte { Floor, Wall, Hole }

    /// <summary>
    /// Static part of a level: walls, holes and wells (targets). Parsed from a text grid:
    /// '#' wall, '.' floor, 'x' hole; UPPERCASE = droplet of that color, lowercase = well that accepts that color.
    /// Color letters: R red, Y yellow, B blue, O orange, P purple, G green, K brown.
    /// </summary>
    public sealed class Board
    {
        public int Width { get; }
        public int Height { get; }
        private readonly Cell[] _cells;
        /// <summary>Well cells in index order; a state's well mask refers to these indexes.</summary>
        public IReadOnlyList<int> Wells { get; }
        public IReadOnlyList<Paint> WellColors { get; }

        private Board(int w, int h, Cell[] cells, List<int> wells, List<Paint> wellColors)
        {
            Width = w; Height = h; _cells = cells; Wells = wells; WellColors = wellColors;
        }

        public Cell this[int pos] => _cells[pos];
        public Cell At(int x, int y) => _cells[y * Width + x];
        public int Pos(int x, int y) => y * Width + x;
        public int X(int pos) => pos % Width;
        public int Y(int pos) => pos / Width;

        /// <summary>Index of the well at pos, or -1.</summary>
        public int WellIndexAt(int pos)
        {
            for (int i = 0; i < Wells.Count; i++) if (Wells[i] == pos) return i;
            return -1;
        }

        public static char Letter(Paint p) => p switch
        {
            Paint.Red => 'R', Paint.Yellow => 'Y', Paint.Blue => 'B', Paint.Orange => 'O',
            Paint.Purple => 'P', Paint.Green => 'G', Paint.Brown => 'K', _ => '?',
        };

        public static Paint FromLetter(char c) => char.ToUpperInvariant(c) switch
        {
            'R' => Paint.Red, 'Y' => Paint.Yellow, 'B' => Paint.Blue, 'O' => Paint.Orange,
            'P' => Paint.Purple, 'G' => Paint.Green, 'K' => Paint.Brown, _ => Paint.None,
        };

        /// <summary>Parses rows into a board and its initial state. Throws FormatException on bad input.</summary>
        public static (Board board, PuzzleState start) Parse(IReadOnlyList<string> rows)
        {
            if (rows == null || rows.Count < 3) throw new FormatException("Level needs at least 3 rows.");
            int w = rows[0].Length, h = rows.Count;
            if (w < 3 || w > 16 || h > 16) throw new FormatException("Level must be 3..16 wide and at most 16 high.");
            var cells = new Cell[w * h];
            var wells = new List<int>();
            var wellColors = new List<Paint>();
            var drops = new List<(int pos, Paint color)>();
            for (int y = 0; y < h; y++)
            {
                if (rows[y].Length != w) throw new FormatException($"Row {y} has length {rows[y].Length}, expected {w}.");
                for (int x = 0; x < w; x++)
                {
                    char c = rows[y][x];
                    int pos = y * w + x;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                    switch (c)
                    {
                        case '#': cells[pos] = Cell.Wall; break;
                        case '.': cells[pos] = Cell.Floor; break;
                        case 'x': cells[pos] = Cell.Hole; break;
                        default:
                            var paint = FromLetter(c);
                            if (paint == Paint.None) throw new FormatException($"Unknown char '{c}' at {x},{y}.");
                            cells[pos] = Cell.Floor;
                            if (char.IsUpper(c)) drops.Add((pos, paint));
                            else { wells.Add(pos); wellColors.Add(paint); }
                            break;
                    }
                    if (edge && cells[pos] != Cell.Wall) throw new FormatException($"Border must be walls (at {x},{y}).");
                }
            }
            if (wells.Count == 0) throw new FormatException("Level has no wells.");
            if (wells.Count > 16) throw new FormatException("At most 16 wells.");
            if (drops.Count == 0) throw new FormatException("Level has no droplets.");
            var board = new Board(w, h, cells, wells, wellColors);
            return (board, PuzzleState.Create(drops, (1 << wells.Count) - 1));
        }
    }

    /// <summary>One droplet. Id is stable across moves so a view can animate it.</summary>
    public readonly struct Droplet
    {
        public readonly int Id;
        public readonly int Pos;
        public readonly Paint Color;
        public Droplet(int id, int pos, Paint color) { Id = id; Pos = pos; Color = color; }
    }

    /// <summary>Immutable dynamic state: droplets (sorted by position) and the wells still open.</summary>
    public sealed class PuzzleState
    {
        public IReadOnlyList<Droplet> Droplets => _drops;
        /// <summary>Bit i set = well i still open.</summary>
        public int OpenWells { get; }
        public bool IsSolved => OpenWells == 0;

        private readonly Droplet[] _drops;
        private string _key;

        private PuzzleState(Droplet[] drops, int openWells)
        {
            Array.Sort(drops, (a, b) => a.Pos.CompareTo(b.Pos));
            _drops = drops;
            OpenWells = openWells;
        }

        internal static PuzzleState Create(List<(int pos, Paint color)> drops, int openWells)
        {
            var arr = new Droplet[drops.Count];
            for (int i = 0; i < drops.Count; i++) arr[i] = new Droplet(i, drops[i].pos, drops[i].color);
            return new PuzzleState(arr, openWells);
        }

        internal static PuzzleState From(List<Droplet> drops, int openWells) => new PuzzleState(drops.ToArray(), openWells);

        public bool IsWellOpen(int wellIndex) => (OpenWells & (1 << wellIndex)) != 0;

        /// <summary>Canonical key for the solver: positions + colors + open wells (ids are irrelevant).</summary>
        public string Key
        {
            get
            {
                if (_key != null) return _key;
                var sb = new StringBuilder(_drops.Length * 3 + 4);
                sb.Append((char)OpenWells);
                foreach (var d in _drops) sb.Append((char)d.Pos).Append((char)d.Color);
                return _key = sb.ToString();
            }
        }
    }

    public enum DropletFate { Moved, Merged, FellInHole, Delivered }

    /// <summary>What happened to one droplet in a tilt, in order of resolution (for animation).</summary>
    public readonly struct DropletStep
    {
        public readonly int Id;
        public readonly int From;
        public readonly int To;
        public readonly DropletFate Fate;
        /// <summary>Merged: id of the droplet it joined. Delivered: well index.</summary>
        public readonly int Other;
        /// <summary>Color after this step (merged target's new color for Merged).</summary>
        public readonly Paint Color;
        public DropletStep(int id, int from, int to, DropletFate fate, int other, Paint color)
        { Id = id; From = from; To = to; Fate = fate; Other = other; Color = color; }
    }

    public sealed class TiltResult
    {
        public PuzzleState State;
        public readonly List<DropletStep> Steps = new List<DropletStep>();
        /// <summary>False when nothing moved: such a tilt does not count as a move.</summary>
        public bool Changed;
    }

    /// <summary>
    /// The rules. A tilt slides every droplet in the direction until it meets a wall. Droplets nearest the wall move
    /// first; a droplet sliding into a resting droplet merges into it (colors mix). A droplet sliding into a hole is lost.
    /// After all droplets rest, each droplet resting on an open well of exactly its color fills that well and disappears.
    /// The level is solved when every well is filled.
    /// </summary>
    public static class Rules
    {
        public static (int dx, int dy) Delta(Tilt t) => t switch
        {
            Tilt.Up => (0, -1), Tilt.Down => (0, 1), Tilt.Left => (-1, 0), _ => (1, 0),
        };

        public static TiltResult Apply(Board board, PuzzleState state, Tilt tilt)
        {
            var (dx, dy) = Delta(tilt);
            var order = new List<Droplet>(state.Droplets);
            // Nearest to the destination wall first.
            order.Sort((a, b) =>
            {
                int ka = board.X(a.Pos) * dx + board.Y(a.Pos) * dy;
                int kb = board.X(b.Pos) * dx + board.Y(b.Pos) * dy;
                return kb.CompareTo(ka);
            });

            var result = new TiltResult();
            var resting = new Dictionary<int, int>(); // pos -> index in settled
            var settled = new List<Droplet>();

            foreach (var d in order)
            {
                int x = board.X(d.Pos), y = board.Y(d.Pos);
                int pos = d.Pos;
                bool done = false;
                while (true)
                {
                    int nx = x + dx, ny = y + dy;
                    int next = board.Pos(nx, ny);
                    var cell = board[next];
                    if (cell == Cell.Wall) break;
                    if (resting.TryGetValue(next, out int hostIdx))
                    {
                        var host = settled[hostIdx];
                        var mixed = host.Color | d.Color;
                        settled[hostIdx] = new Droplet(host.Id, host.Pos, mixed);
                        result.Steps.Add(new DropletStep(d.Id, d.Pos, next, DropletFate.Merged, host.Id, mixed));
                        result.Changed = true;
                        done = true;
                        break;
                    }
                    x = nx; y = ny; pos = next;
                    if (cell == Cell.Hole)
                    {
                        result.Steps.Add(new DropletStep(d.Id, d.Pos, pos, DropletFate.FellInHole, -1, d.Color));
                        result.Changed = true;
                        done = true;
                        break;
                    }
                }
                if (done) continue;
                if (pos != d.Pos)
                {
                    result.Changed = true;
                    result.Steps.Add(new DropletStep(d.Id, d.Pos, pos, DropletFate.Moved, -1, d.Color));
                }
                resting[pos] = settled.Count;
                settled.Add(new Droplet(d.Id, pos, d.Color));
            }

            // Deliveries happen only after everything rests.
            int open = state.OpenWells;
            var remaining = new List<Droplet>(settled.Count);
            foreach (var d in settled)
            {
                int w = board.WellIndexAt(d.Pos);
                if (w >= 0 && (open & (1 << w)) != 0 && board.WellColors[w] == d.Color)
                {
                    open &= ~(1 << w);
                    result.Steps.Add(new DropletStep(d.Id, d.Pos, d.Pos, DropletFate.Delivered, w, d.Color));
                    result.Changed = true;
                }
                else remaining.Add(d);
            }

            result.State = result.Changed ? PuzzleState.From(remaining, open) : state;
            return result;
        }

        /// <summary>
        /// Cheap necessary condition for solvability: every open well's color must be buildable from droplets whose
        /// colors are subsets of it (mixing only adds primaries). False = certainly stuck (restart / undo).
        /// </summary>
        public static bool CanStillSolve(Board board, PuzzleState state)
        {
            for (int w = 0; w < board.Wells.Count; w++)
            {
                if (!state.IsWellOpen(w)) continue;
                var need = board.WellColors[w];
                Paint have = Paint.None;
                foreach (var d in state.Droplets) if ((d.Color & ~need) == 0) have |= d.Color;
                if (have != need) return false;
            }
            return true;
        }
    }

    /// <summary>Breadth-first search for the shortest solution.</summary>
    public static class Solver
    {
        private static readonly Tilt[] AllTilts = { Tilt.Up, Tilt.Down, Tilt.Left, Tilt.Right };

        public sealed class Result
        {
            /// <summary>null when no solution was found within the limits.</summary>
            public List<Tilt> Moves;
            public int StatesExplored;
            /// <summary>True when the search stopped at a limit (so "no solution" is not proven).</summary>
            public bool HitLimit;
            public bool Solved => Moves != null;
        }

        public static Result Solve(Board board, PuzzleState start, int maxDepth = 40, int maxStates = 300_000)
        {
            var result = new Result();
            if (start.IsSolved) { result.Moves = new List<Tilt>(); return result; }
            var parent = new Dictionary<string, (string prev, Tilt tilt)> { [start.Key] = (null, Tilt.Up) };
            var frontier = new List<PuzzleState> { start };
            for (int depth = 1; depth <= maxDepth && frontier.Count > 0; depth++)
            {
                var next = new List<PuzzleState>();
                foreach (var s in frontier)
                {
                    foreach (var t in AllTilts)
                    {
                        var r = Rules.Apply(board, s, t);
                        if (!r.Changed) continue;
                        var key = r.State.Key;
                        if (parent.ContainsKey(key)) continue;
                        parent[key] = (s.Key, t);
                        result.StatesExplored++;
                        if (r.State.IsSolved)
                        {
                            result.Moves = Path(parent, key);
                            return result;
                        }
                        if (!Rules.CanStillSolve(board, r.State)) continue;
                        if (parent.Count >= maxStates) { result.HitLimit = true; return result; }
                        next.Add(r.State);
                    }
                }
                frontier = next;
                if (depth == maxDepth && frontier.Count > 0) result.HitLimit = true;
            }
            return result;
        }

        private static List<Tilt> Path(Dictionary<string, (string prev, Tilt tilt)> parent, string key)
        {
            var moves = new List<Tilt>();
            while (parent[key].prev != null)
            {
                moves.Add(parent[key].tilt);
                key = parent[key].prev;
            }
            moves.Reverse();
            return moves;
        }
    }
}
