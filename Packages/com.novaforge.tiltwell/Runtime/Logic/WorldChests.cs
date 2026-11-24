using System;
using System.Collections.Generic;

namespace NovaForge.Tiltwell
{
    /// <summary>A reward chest at the end of each world (tier): opens once, when every level of the world is cleared.</summary>
    public static class WorldChest
    {
        /// <summary>Coins and hints in the chest of a world. Later worlds give more.</summary>
        public static (int coins, int hints) Reward(int tier) => (100 + 50 * Math.Max(1, tier), 1 + Math.Max(1, tier) / 3);

        public static bool IsWorldComplete(IReadOnlyList<LevelData> levels, int tier, Func<string, bool> isCompleted)
        {
            bool any = false;
            foreach (var l in levels)
            {
                if (l.Tier != tier) continue;
                any = true;
                if (!isCompleted(l.Id)) return false;
            }
            return any;
        }
    }

    /// <summary>Which world chests were opened (saved by the game).</summary>
    public sealed class WorldChestState
    {
        public List<int> Opened = new List<int>();

        public bool IsOpened(int tier) => Opened.Contains(tier);

        /// <returns>false if it was already open.</returns>
        public bool Open(int tier)
        {
            if (IsOpened(tier)) return false;
            Opened.Add(tier);
            return true;
        }
    }
}
