using System.Collections.Generic;

namespace NovaForge.Tiltwell
{
    /// <summary>How droplets are drawn. Cosmetic only.</summary>
    public enum DropSkin { Classic, Eyes, Jelly, Bubble, Star }

    /// <summary>Skin catalog: coin prices (Classic is free and always owned).</summary>
    public static class DropSkins
    {
        public static readonly DropSkin[] All = { DropSkin.Classic, DropSkin.Eyes, DropSkin.Jelly, DropSkin.Bubble, DropSkin.Star };

        public static int Price(DropSkin skin) => skin switch
        {
            DropSkin.Eyes => 300,
            DropSkin.Jelly => 500,
            DropSkin.Bubble => 800,
            DropSkin.Star => 1200,
            _ => 0,
        };

        public static string Id(DropSkin skin) => "skin_" + skin.ToString().ToLowerInvariant();
    }

    /// <summary>Owned and equipped skins (saved by the game).</summary>
    public sealed class SkinState
    {
        public List<DropSkin> Owned = new List<DropSkin>();
        public DropSkin Equipped = DropSkin.Classic;

        public bool Owns(DropSkin skin) => skin == DropSkin.Classic || Owned.Contains(skin);

        /// <returns>false if already owned.</returns>
        public bool Add(DropSkin skin)
        {
            if (Owns(skin)) return false;
            Owned.Add(skin);
            return true;
        }

        /// <returns>false if the skin is not owned.</returns>
        public bool Equip(DropSkin skin)
        {
            if (!Owns(skin)) return false;
            Equipped = skin;
            return true;
        }
    }
}
