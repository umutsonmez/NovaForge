using System.Collections.Generic;

namespace NovaForge.Tiltwell
{
    public enum PuzzleStatus { Playing, Solved, OutOfMoves, Stuck }

    /// <summary>
    /// One attempt at a level: current state, move count, undo history, hints. Engine-free; the Unity view only
    /// forwards input and animates the returned <see cref="TiltResult"/>.
    /// </summary>
    public sealed class PuzzleSession
    {
        private readonly Stack<PuzzleState> _history = new Stack<PuzzleState>();

        public LevelData Level { get; }
        public Board Board { get; }
        public PuzzleState Start { get; }
        public PuzzleState State { get; private set; }
        public int Moves { get; private set; }
        public int MoveLimit => Level.MoveLimit + _bonusMoves;
        public int MovesLeft => MoveLimit - Moves;
        public bool CanUndo => _history.Count > 0 && (Status == PuzzleStatus.Playing || Status == PuzzleStatus.Stuck);
        private int _bonusMoves;

        public PuzzleStatus Status
        {
            get
            {
                if (State.IsSolved) return PuzzleStatus.Solved;
                if (Moves >= MoveLimit) return PuzzleStatus.OutOfMoves;
                if (!Rules.CanStillSolve(Board, State)) return PuzzleStatus.Stuck;
                return PuzzleStatus.Playing;
            }
        }

        public PuzzleSession(LevelData level)
        {
            Level = level;
            (Board, Start) = Board.Parse(level.Rows);
            State = Start;
        }

        /// <returns>The result to animate, or null when the input was ignored (finished, or nothing moved).</returns>
        public TiltResult Tilt(Tilt tilt)
        {
            var status = Status;
            if (status == PuzzleStatus.Solved || status == PuzzleStatus.OutOfMoves) return null;
            var result = Rules.Apply(Board, State, tilt);
            if (!result.Changed) return null;
            _history.Push(State);
            State = result.State;
            Moves++;
            return result;
        }

        /// <summary>Restores the previous state and gives the move back. Free, so experimenting is never punished.</summary>
        public bool Undo()
        {
            if (!CanUndo) return false;
            State = _history.Pop();
            Moves--;
            return true;
        }

        public void Restart()
        {
            _history.Clear();
            State = Start;
            Moves = 0;
            _bonusMoves = 0;
        }

        /// <summary>Grants extra moves (e.g. rewarded ad "extra moves" after running out).</summary>
        public void AddMoves(int extra)
        {
            if (extra > 0) _bonusMoves += extra; // limit grows; stars still count every move made
        }

        /// <summary>The next move of a shortest solution from the current state; null when none exists (undo/restart).</summary>
        public Tilt? Hint()
        {
            if (Status != PuzzleStatus.Playing) return null;
            var sol = Solver.Solve(Board, State, Level.Par + 12, 200_000);
            return sol.Solved && sol.Moves.Count > 0 ? sol.Moves[0] : (Tilt?)null;
        }

        public int Stars => Level.StarsFor(Moves);
    }
}
