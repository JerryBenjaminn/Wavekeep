using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.Gear
{
    /// <summary>
    /// A unique, MUTABLE, persisted gear item (Task 67 — the pivot from "owned = count of a shared SO" to
    /// "owned = a per-item instance"). References its read-only <see cref="GearBaseSO"/> template, its current
    /// rarity, and the affixes rolled onto it; computes the full <see cref="StatModifier"/> set + Luck bonus on
    /// demand in the EXACT shape the combat pipeline already consumes — so <c>AbilityRuntime</c>/<c>HeroRuntime</c>
    /// need no changes. SOs are never mutated (CLAUDE.md §3.5); all mutable state (rarity, affixes) lives here.
    ///
    /// Member names mirror the old <c>LootItemSO</c> (ItemId/ItemName/Icon/Rarity/Slot/StatModifiers/LuckBonus)
    /// so existing inventory/loadout/Hub-UI code reads it the same way. <see cref="ItemId"/> is the per-instance
    /// GUID — identity is the INSTANCE, not the template (two "Rare Helmets" are distinct objects).
    ///
    /// Mutation rule (locked, even though reroll/upgrade-rarity land in later tasks): mutators here only ADD
    /// affixes or REPLACE a single chosen slot — they never drop or risk existing affixes. The affix list is
    /// exposed read-only; a future mutation calls <see cref="InvalidateCache"/> to rebuild the derived stats.
    /// </summary>
    public sealed class GearInstance
    {
        private readonly List<RolledAffix> _affixes;
        private List<StatModifier> _modCache;
        private float _luckCache;
        private bool _cacheBuilt;

        /// <summary>Per-instance GUID — the unit of identity for ownership, equip, and saves.</summary>
        public string ItemId { get; }
        public GearBaseSO Base { get; }
        public Rarity Rarity { get; private set; }

        public GearInstance(string instanceId, GearBaseSO baseTemplate, Rarity rarity, List<RolledAffix> affixes)
        {
            ItemId = string.IsNullOrEmpty(instanceId) ? System.Guid.NewGuid().ToString("N") : instanceId;
            Base = baseTemplate;
            Rarity = rarity;
            _affixes = affixes ?? new List<RolledAffix>();
        }

        /// <summary>Create a brand-new instance (fresh GUID). Used by the debug spawn now and by drop generation later.</summary>
        public static GearInstance Create(GearBaseSO baseTemplate, Rarity rarity, List<RolledAffix> affixes) =>
            new GearInstance(System.Guid.NewGuid().ToString("N"), baseTemplate, rarity, affixes);

        public IReadOnlyList<RolledAffix> Affixes => _affixes;

        // --- LootItemSO-compatible accessors (so existing consumers read it unchanged) ----------------

        public GearSlot Slot => Base != null ? Base.Slot : GearSlot.Helmet;
        public Sprite Icon => Base != null ? Base.Icon : null;

        public string ItemName
        {
            get
            {
                string baseName = Base != null && !string.IsNullOrEmpty(Base.DisplayName)
                    ? Base.DisplayName
                    : (Base != null ? Base.Slot.ToString() : "Gear");
                return $"{Rarity} {baseName}";
            }
        }

        /// <summary>The combat stat modifiers (implicit + stat-modifier affixes), in the shape <c>HeroLoadout</c>
        /// aggregates and <c>AbilityRuntime</c> consumes. Luck is NOT here (see <see cref="LuckBonus"/>). Cached;
        /// rebuilt on mutation.</summary>
        public IReadOnlyList<StatModifier> StatModifiers { get { EnsureCache(); return _modCache; } }

        /// <summary>Summed Luck from the implicit and/or affixes, kept separate from combat modifiers exactly like
        /// the old per-item LuckBonus the loadout sums. Cached.</summary>
        public float LuckBonus { get { EnsureCache(); return _luckCache; } }

        /// <summary>Invalidate the derived stat/luck cache after a (future) mutation so it rebuilds on next read.</summary>
        internal void InvalidateCache() => _cacheBuilt = false;

        // --- modifier resolution ----------------------------------------------------------------------

        private void EnsureCache()
        {
            if (_cacheBuilt) return;
            _modCache = new List<StatModifier>();
            _luckCache = 0f;

            if (Base != null)
                ApplyStat(Base.ImplicitStat, Base.ImplicitValue(Rarity));

            for (int i = 0; i < _affixes.Count; i++)
            {
                var def = _affixes[i]?.Definition;
                if (def == null) continue;
                // Only the StatModifier kind is implemented; a future proc/status kind branches here.
                if (def.Effect.Kind == GearEffectKind.StatModifier)
                    ApplyStat(def.Effect.Stat, _affixes[i].Value);
            }

            _cacheBuilt = true;
        }

        private void ApplyStat(GearStatType stat, float value)
        {
            if (stat == GearStatType.Luck) { _luckCache += value; return; }
            _modCache.Add(new StatModifier(ToAbilityModifierType(stat), value));
        }

        private static AbilityModifierType ToAbilityModifierType(GearStatType stat)
        {
            switch (stat)
            {
                case GearStatType.DamageMultiplier: return AbilityModifierType.DamageMultiplier;
                case GearStatType.DamageFlatBonus: return AbilityModifierType.DamageFlatBonus;
                case GearStatType.CooldownMultiplier: return AbilityModifierType.CooldownMultiplier;
                case GearStatType.RangeMultiplier: return AbilityModifierType.RangeMultiplier;
                default: return AbilityModifierType.DamageFlatBonus; // Luck is handled before this; safe fallback
            }
        }
    }
}
