namespace Wavekeep.Data
{
    /// <summary>
    /// The KIND of effect an affix applies (Task 67). Intentionally a single-value enum today —
    /// <see cref="StatModifier"/> is all that is implemented — but it is the explicit extensibility point:
    /// a future proc/status affix is a NEW enum value plus a new branch where effects are applied, with NO
    /// change to the persisted affix shape (which stores an affixId + a rolled value). Keeping the kind
    /// explicit now avoids a save-format migration when that day comes (a reviewer-blocking requirement).
    /// </summary>
    public enum GearEffectKind
    {
        StatModifier
        // Future (not this task): Proc, StatusOnHit, ...
    }
}
