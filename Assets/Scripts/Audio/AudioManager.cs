using System;
using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Core;

namespace Wavekeep.Audio
{
    /// <summary>
    /// Task 59: the run-scoped audio playback engine, owned by <see cref="GameSession"/> (no static singleton,
    /// §3.5) and built by GameSessionBootstrap exactly like CurrencyManager/EnemyPool. It is a pure engine —
    /// "what plays when" lives in the <see cref="AudioEventListener"/> it owns + the <see cref="AudioConfigSO"/>;
    /// this class only knows "how to play".
    ///
    /// It pre-warms a small pool of one-shot SFX <see cref="AudioSource"/>s (no Instantiate/Destroy per sound,
    /// §3.5), a pool of looping sources handed out by integer handle (e.g. a hero Ultimate's active-zone loop),
    /// and two dedicated music sources for crossfading. All sources hang off one hidden child GameObject so
    /// teardown is a single Destroy. Routing goes through the cue's category → mixer group (AudioConfigSO).
    ///
    /// Cross-platform (§3.6): plain 2D AudioSources, no PC-only assumptions; the position parameter on
    /// <see cref="PlayCueAtPosition"/> is stored for future spatial use but no distance attenuation is added yet.
    /// </summary>
    public sealed class AudioManager : IDisposable
    {
        private readonly AudioConfigSO _config;
        private readonly GameObject _root;

        private readonly List<AudioSource> _sfxPool = new List<AudioSource>();
        private readonly List<AudioSource> _loopPool = new List<AudioSource>();
        private readonly Dictionary<int, AudioSource> _activeLoops = new Dictionary<int, AudioSource>();
        private int _nextLoopHandle = 1;

        private readonly AudioSource _musicA;
        private readonly AudioSource _musicB;
        private AudioSource _activeMusic;

        // Music crossfade state (advanced by Tick — AudioManager has no Update of its own).
        private bool _crossfading;
        private float _crossfadeTimer;
        private float _crossfadeDuration;
        private float _fadeInTarget;
        private float _fadeOutStart;
        private AudioSource _fadeIn;
        private AudioSource _fadeOut;

        private readonly AudioEventListener _listener;

        public AudioManager(EventBus events, Transform parent, AudioConfigSO config)
        {
            _config = config;

            _root = new GameObject("[AudioManager]");
            if (parent != null) _root.transform.SetParent(parent, false);

            int sfxCount = config != null ? Mathf.Max(1, config.SfxPoolSize) : 8;
            for (int i = 0; i < sfxCount; i++) _sfxPool.Add(CreateSource($"SFX_{i}"));

            int loopCount = config != null ? Mathf.Max(1, config.LoopSourcePoolSize) : 4;
            for (int i = 0; i < loopCount; i++)
            {
                var s = CreateSource($"Loop_{i}");
                s.loop = true;
                _loopPool.Add(s);
            }

            _musicA = CreateSource("Music_A");
            _musicB = CreateSource("Music_B");
            _activeMusic = _musicA;

            // The manager OWNS the event→cue listener (Task 59 §2.4). Built last so the engine is ready first.
            _listener = new AudioEventListener(events, this, config);
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D for now; spatialisation is future scope (§3 out-of-scope)
            return source;
        }

        // --- One-shot SFX ------------------------------------------------------------------------------------

        /// <summary>Play a one-shot cue on a pooled SFX source. No-op for a null/clip-less cue.</summary>
        public void PlayCue(AudioCueDefinitionSO cue) => PlayOneShot(cue, false, Vector3.zero);

        /// <summary>Play a one-shot cue at a world position (stored for future spatial audio; currently 2D).</summary>
        public void PlayCueAtPosition(AudioCueDefinitionSO cue, Vector3 position) => PlayOneShot(cue, true, position);

        private void PlayOneShot(AudioCueDefinitionSO cue, bool positioned, Vector3 position)
        {
            if (cue == null) return;
            var clip = cue.PickClip();
            if (clip == null) return; // placeholder cue with no clips yet

            // Reuse a free pooled source, or steal the first one if all are busy (a brief one-shot cut is
            // better than dropping the sound) — still no Instantiate/Destroy (§3.5).
            var source = GetFreeSource(_sfxPool) ?? (_sfxPool.Count > 0 ? _sfxPool[0] : null);
            if (source == null) return;

            Configure(source, cue, clip, loop: false);
            if (positioned) source.transform.position = position;
            source.Play();
        }

        // --- Looping cues (handle-based) -------------------------------------------------------------------

        /// <summary>Start a looping cue and return a handle to stop it later. Returns 0 if it can't play
        /// (null/clip-less cue, or no free loop source — raise <c>LoopSourcePoolSize</c> if that happens).</summary>
        public int StartLoopingCue(AudioCueDefinitionSO cue)
        {
            if (cue == null) return 0;
            var clip = cue.PickClip();
            if (clip == null) return 0;

            var source = GetFreeSource(_loopPool);
            if (source == null) return 0;

            Configure(source, cue, clip, loop: true);
            source.Play();

            int handle = _nextLoopHandle++;
            _activeLoops[handle] = source;
            return handle;
        }

        /// <summary>Stop a looping cue started by <see cref="StartLoopingCue"/>. Safe for an unknown/0 handle.</summary>
        public void StopLoopingCue(int handle)
        {
            if (handle == 0 || !_activeLoops.TryGetValue(handle, out var source)) return;
            source.Stop();
            source.clip = null;
            _activeLoops.Remove(handle);
        }

        // --- Music -----------------------------------------------------------------------------------------

        /// <summary>Switch the background music track, optionally crossfading from the current one over the
        /// config's crossfade duration. Honours the cue's own Loop flag (a victory sting can be one-shot).
        /// No-op for a null/clip-less cue.</summary>
        public void PlayMusicTrack(AudioCueDefinitionSO musicCue, bool crossfade)
        {
            if (musicCue == null) return;
            var clip = musicCue.PickClip();
            if (clip == null) return;

            var next = _activeMusic == _musicA ? _musicB : _musicA;
            next.clip = clip;
            next.loop = musicCue.Loop;
            next.pitch = musicCue.PickPitch();
            next.outputAudioMixerGroup = _config != null ? _config.GroupFor(musicCue.Category) : null;

            float duration = _config != null ? _config.MusicCrossfadeDuration : 1.5f;
            if (crossfade && _activeMusic.isPlaying && duration > 0.01f)
            {
                next.volume = 0f;
                next.Play();
                _fadeIn = next;
                _fadeInTarget = musicCue.Volume;
                _fadeOut = _activeMusic;
                _fadeOutStart = _activeMusic.volume;
                _crossfadeDuration = duration;
                _crossfadeTimer = 0f;
                _crossfading = true;
            }
            else
            {
                _activeMusic.Stop();
                next.volume = musicCue.Volume;
                next.Play();
                _crossfading = false;
            }

            _activeMusic = next;
        }

        /// <summary>Advance the music crossfade. Called once per frame by GameSessionBootstrap (AudioManager has
        /// no Update). Cheap no-op when nothing is crossfading.</summary>
        public void Tick(float deltaTime)
        {
            if (!_crossfading) return;

            _crossfadeTimer += deltaTime;
            float t = _crossfadeDuration > 0f ? Mathf.Clamp01(_crossfadeTimer / _crossfadeDuration) : 1f;

            if (_fadeIn != null) _fadeIn.volume = Mathf.Lerp(0f, _fadeInTarget, t);
            if (_fadeOut != null) _fadeOut.volume = Mathf.Lerp(_fadeOutStart, 0f, t);

            if (t >= 1f)
            {
                if (_fadeOut != null)
                {
                    _fadeOut.Stop();
                    _fadeOut.volume = _fadeOutStart; // restore so this source is clean for its next use
                }
                _crossfading = false;
                _fadeIn = null;
                _fadeOut = null;
            }
        }

        // --- Helpers ---------------------------------------------------------------------------------------

        private void Configure(AudioSource source, AudioCueDefinitionSO cue, AudioClip clip, bool loop)
        {
            source.clip = clip;
            source.volume = cue.Volume;
            source.pitch = cue.PickPitch();
            source.loop = loop;
            source.outputAudioMixerGroup = _config != null ? _config.GroupFor(cue.Category) : null;
        }

        private static AudioSource GetFreeSource(List<AudioSource> pool)
        {
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] != null && !pool[i].isPlaying) return pool[i];
            return null; // none free — callers decide (SFX steals, loops report failure)
        }

        public void Dispose()
        {
            _listener?.Dispose();
            _activeLoops.Clear();
            if (_root != null) UnityEngine.Object.Destroy(_root); // tears down every pooled AudioSource at once
        }
    }
}
