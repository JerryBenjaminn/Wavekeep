using UnityEngine;
using UnityEngine.Audio;

namespace Wavekeep.Audio
{
    /// <summary>
    /// Task 59: the single designer-authored config the <see cref="AudioManager"/> reads — the mixer groups it
    /// routes through, the AudioSource pool sizes, and the placeholder cue per existing EventBus event. Keeping
    /// these on one SO (referenced by GameSessionBootstrap) means the audio wiring is data-driven and the manager
    /// stays a pure playback engine. All cue references are clip-less placeholders until the developer fills them.
    /// Read-only at runtime (§3.5).
    /// </summary>
    [CreateAssetMenu(fileName = "WavekeepAudioConfig", menuName = "Wavekeep/Audio Config")]
    public sealed class AudioConfigSO : ScriptableObject
    {
        [Header("Mixer Groups (assign from the WavekeepAudioMixer)")]
        [SerializeField] private AudioMixerGroup _musicGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _uiGroup;

        [Header("AudioSource Pools")]
        [Tooltip("Pre-warmed one-shot SFX sources (no Instantiate/Destroy per sound, §3.5).")]
        [SerializeField, Min(1)] private int _sfxPoolSize = 16;
        [Tooltip("Pre-warmed looping sources (e.g. one per active Ultimate-zone loop), handed out by handle.")]
        [SerializeField, Min(1)] private int _loopSourcePoolSize = 4;

        [Header("Music")]
        [SerializeField, Min(0f)] private float _musicCrossfadeDuration = 1.5f;

        [Header("Scene Ambience (looped on scene/run start by the bootstrap)")]
        [Tooltip("Looping background music for this scene (gameplay/hub). Plays on start; the victory/defeat cues " +
                 "crossfade over it on run end.")]
        [SerializeField] private AudioCueDefinitionSO _backgroundMusicCue;
        [Tooltip("Looping ambient SFX bed (wind, battlefield hum, …). Plays on start, runs until the scene unloads.")]
        [SerializeField] private AudioCueDefinitionSO _ambientCue;

        [Header("Event Cues (placeholders — drop clips onto each cue asset)")]
        [SerializeField] private AudioCueDefinitionSO _enemyKilledCue;
        [SerializeField] private AudioCueDefinitionSO _waveStartedCue;
        [SerializeField] private AudioCueDefinitionSO _waveCompletedCue;
        [SerializeField] private AudioCueDefinitionSO _levelUpCue;
        [SerializeField] private AudioCueDefinitionSO _victoryCue;
        [SerializeField] private AudioCueDefinitionSO _defeatCue;

        public int SfxPoolSize => _sfxPoolSize;
        public int LoopSourcePoolSize => _loopSourcePoolSize;
        public float MusicCrossfadeDuration => _musicCrossfadeDuration;

        public AudioCueDefinitionSO BackgroundMusicCue => _backgroundMusicCue;
        public AudioCueDefinitionSO AmbientCue => _ambientCue;

        public AudioCueDefinitionSO EnemyKilledCue => _enemyKilledCue;
        public AudioCueDefinitionSO WaveStartedCue => _waveStartedCue;
        public AudioCueDefinitionSO WaveCompletedCue => _waveCompletedCue;
        public AudioCueDefinitionSO LevelUpCue => _levelUpCue;
        public AudioCueDefinitionSO VictoryCue => _victoryCue;
        public AudioCueDefinitionSO DefeatCue => _defeatCue;

        /// <summary>The mixer group a cue of <paramref name="category"/> routes through (null = default output).</summary>
        public AudioMixerGroup GroupFor(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Music: return _musicGroup;
                case AudioCategory.UI: return _uiGroup;
                default: return _sfxGroup;
            }
        }
    }
}
