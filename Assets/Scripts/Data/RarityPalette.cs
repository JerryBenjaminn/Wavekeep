using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Single source of truth for the rarity → colour mapping (Task 69). Used by both the arena visual loot
    /// drop and the end-of-run summary panel so a tier always reads as the same colour. Pure presentation —
    /// no gameplay meaning. Matches the looter convention in the task: grey / green / blue / purple / orange / red.
    /// </summary>
    public static class RarityPalette
    {
        public static Color Color(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:    return new Color(0.72f, 0.72f, 0.72f);
                case Rarity.Uncommon:  return new Color(0.35f, 0.90f, 0.35f);
                case Rarity.Rare:      return new Color(0.30f, 0.55f, 1.00f);
                case Rarity.Epic:      return new Color(0.70f, 0.35f, 1.00f);
                case Rarity.Legendary: return new Color(1.00f, 0.60f, 0.10f);
                case Rarity.Unique:    return new Color(1.00f, 0.25f, 0.20f);
                default:               return UnityEngine.Color.white;
            }
        }

        /// <summary>Hex (RRGGBB, no '#') for the rarity colour — for TMP rich-text <c>&lt;color&gt;</c> tags.</summary>
        public static string Hex(Rarity rarity) => ColorUtility.ToHtmlStringRGB(Color(rarity));
    }
}
