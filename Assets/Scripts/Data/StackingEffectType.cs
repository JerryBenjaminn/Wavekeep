namespace Wavekeep.Data
{
    /// <summary>
    /// The small, fixed set of STACKING status effects an ability can build up on an enemy (Task 19),
    /// distinct from the simple timed <see cref="StatusEffectType"/> set (Freeze/Slow/Burn). A stacking
    /// effect accumulates a per-enemy counter that decays over time and triggers a one-shot payload
    /// (e.g. a Freeze) when it reaches its max. Kept as its own closed enum — handled once by the
    /// generic stacking-effect machine on <c>EnemyRuntime</c>, so a future hero can reuse the same
    /// structure with different parameters by adding a value here, not new bespoke fields.
    /// </summary>
    public enum StackingEffectType
    {
        Frost
    }
}
