using UnityEngine;

namespace Wavekeep.Audio
{
    /// <summary>
    /// Task 59: designer-authored definition of one playable sound cue (CLAUDE.md §3.1). Read-only template —
    /// it holds no playback state (CLAUDE.md §3.5); the live AudioSource and any looping handle live in
    /// <see cref="AudioManager"/>, never written back here.
    ///
    /// Clips are an ARRAY so a single cue can hold a random-variation pool (e.g. several hit-grunt takes).
    /// Created clip-less by the Task 59 setup; the developer drops real <see cref="AudioClip"/>s in later.
    /// </summary>
    [CreateAssetMenu(fileName = "Cue_NewSound", menuName = "Wavekeep/Audio Cue")]
    public sealed class AudioCueDefinitionSO : ScriptableObject
    {
        [Tooltip("Random-variation pool. One clip is chosen at random each play (single clip = no variation). " +
                 "Leave empty until real audio is assigned — playing an empty cue is a safe no-op.")]
        [SerializeField] private AudioClip[] _clips;

        [Tooltip("Routing category → mixer group (Music/SFX/UI), resolved by AudioManager via AudioConfigSO.")]
        [SerializeField] private AudioCategory _category = AudioCategory.SFX;

        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        [Tooltip("Min/max pitch multiplier; a random value in this range is used per play. (1,1) = no variation.")]
        [SerializeField] private Vector2 _pitchRandomRange = new Vector2(1f, 1f);

        [Tooltip("Sustained cue (e.g. a hero Ultimate's active-zone loop). One-shots leave this off.")]
        [SerializeField] private bool _loop;

        public AudioCategory Category => _category;
        public float Volume => _volume;
        public bool Loop => _loop;

        /// <summary>True if this cue currently has at least one clip — playing a clip-less placeholder no-ops.</summary>
        public bool HasClips => _clips != null && _clips.Length > 0;

        /// <summary>Pick a clip for this play (random from the pool), or null if none assigned yet. Pure read —
        /// no SO state is mutated (§3.5).</summary>
        public AudioClip PickClip()
        {
            if (_clips == null || _clips.Length == 0) return null;
            if (_clips.Length == 1) return _clips[0];
            return _clips[Random.Range(0, _clips.Length)];
        }

        /// <summary>Pick a pitch for this play (random within the configured range, clamped sane).</summary>
        public float PickPitch()
        {
            float lo = _pitchRandomRange.x;
            float hi = _pitchRandomRange.y;
            if (hi <= lo) return lo > 0f ? lo : 1f;
            return Random.Range(lo, hi);
        }
    }
}
