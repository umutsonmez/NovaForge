using System.Collections.Generic;

namespace NovaForge.Tiltwell
{
    /// <summary>Which primaries make a color, and which mixed colors a level asks for. Used by the tutorial UI.</summary>
    public static class ColorRecipes
    {
        public static readonly Paint[] PrimaryColors = { Paint.Red, Paint.Yellow, Paint.Blue };

        /// <summary>The mixed colors in the order the color guide teaches them.</summary>
        public static readonly Paint[] MixedColors = { Paint.Orange, Paint.Purple, Paint.Green, Paint.Brown };

        public static bool IsMixed(Paint p) => p != Paint.None && (p & (p - 1)) != 0;

        /// <summary>Primaries that combine into <paramref name="color"/> (a primary returns itself).</summary>
        public static List<Paint> Ingredients(Paint color)
        {
            var list = new List<Paint>(3);
            foreach (var p in PrimaryColors) if ((color & p) != 0) list.Add(p);
            return list;
        }

        /// <summary>Distinct mixed well colors in a level, in teaching order.</summary>
        public static List<Paint> MixedWells(LevelData level)
        {
            var found = new HashSet<Paint>();
            foreach (var row in level.Rows)
                foreach (var c in row)
                    if (char.IsLower(c) && c != 'x')
                    {
                        var p = Board.FromLetter(c);
                        if (IsMixed(p)) found.Add(p);
                    }
            var ordered = new List<Paint>(found.Count);
            foreach (var p in MixedColors) if (found.Contains(p)) ordered.Add(p);
            return ordered;
        }
    }
}
