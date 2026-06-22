namespace Wavekeep.Data
{
    /// <summary>
    /// The fixed, small set of enemy status effects an upgrade can apply on hit (CLAUDE.md §3.8,
    /// Task 11). Deliberately a closed enum (not an open scripting system): each value is handled once
    /// by the generic status-effect state machine on <c>EnemyRuntime</c>. Magnitude/duration are
    /// SO-authored data, never hardcoded.
    ///
    /// Magnitude semantics (documented):
    /// <list type="bullet">
    /// <item><see cref="Freeze"/> — movement speed → 0 for the duration. Magnitude is unused (hard stop).</item>
    /// <item><see cref="Slow"/> — movement speed × (1 − magnitude). Magnitude is the slow fraction [0..1].</item>
    /// <item><see cref="Burn"/> — magnitude damage per tick (via EnemyRuntime.TakeDamage) over the duration.</item>
    /// </list>
    /// </summary>
    public enum StatusEffectType
    {
        Freeze,
        Slow,
        Burn
    }
}
