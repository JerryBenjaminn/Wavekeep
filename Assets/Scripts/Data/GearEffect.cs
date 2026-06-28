using System;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// The extensible, sealed-shape description of what an affix DOES (Task 67). Today only the
    /// <see cref="GearEffectKind.StatModifier"/> kind is meaningful, carrying which <see cref="GearStatType"/>
    /// it modifies; the rolled magnitude is supplied separately (an affix's value range, or a base's per-rarity
    /// implicit value). A future proc/status kind adds fields here plus a branch at the apply site, without
    /// reshaping saved data. A plain serializable struct so it lives directly on the affix SO.
    /// </summary>
    [Serializable]
    public struct GearEffect
    {
        [SerializeField] private GearEffectKind _kind;
        [Tooltip("Which live stat this effect modifies (meaningful when Kind == StatModifier).")]
        [SerializeField] private GearStatType _stat;

        public GearEffectKind Kind => _kind;
        public GearStatType Stat => _stat;

        public GearEffect(GearEffectKind kind, GearStatType stat)
        {
            _kind = kind;
            _stat = stat;
        }
    }
}
