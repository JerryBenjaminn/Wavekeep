namespace Wavekeep.Audio
{
    /// <summary>
    /// Task 59: routing category for an <see cref="AudioCueDefinitionSO"/>. The cue carries this enum (rather
    /// than a direct AudioMixerGroup reference) so cue assets stay decoupled from the concrete mixer asset —
    /// <see cref="AudioManager"/> maps the category to the matching mixer group at play time via
    /// <see cref="AudioConfigSO"/>. Chosen over a per-cue AudioMixerGroup field because it avoids assigning the
    /// mixer group on every cue, keeps the routing in one place, and lets the mixer be swapped without touching
    /// every cue (documented choice per Task 59 §2.1).
    /// </summary>
    public enum AudioCategory
    {
        Music,
        SFX,
        UI
    }
}
