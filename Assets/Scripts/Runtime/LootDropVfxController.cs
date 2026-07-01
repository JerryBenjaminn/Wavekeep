using System.Collections.Generic;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 69/70: the physical, colour-coded arena loot drop (replaces the old <c>LootDropHud</c> text toast).
    /// When a kill rolls gear, <see cref="GearDroppedEvent"/> fires with the death position; this controller spawns
    /// a small, transient GLOWING CUBE (looter-shooter "engram" style) at that spot, tinted by the rolled rarity,
    /// that fades in, hovers/spins/bobs for a hold window, then fades out. PURELY visual — there is no pickup; the
    /// instance was already granted by <c>LootService</c>.
    ///
    /// Task 70 changed the marker SHAPE (was a vertical beam + ground ring) to a glowing cube and extended its
    /// visible duration (~1s → see <see cref="_holdSeconds"/>). The hook, trigger event, and pooling are unchanged.
    ///
    /// It hooks the SAME existing drop event (no parallel drop-detection path) and owns its own runtime marker
    /// objects, parented to THIS controller — never to the pooled enemy — so <c>EnemyPoolManager</c> recycling a
    /// corpse can never disturb or leak a marker. Markers are pooled internally and reused; nothing persists past
    /// the fade. Colour-per-marker is set via a <c>MaterialPropertyBlock</c> over a single shared unlit material
    /// (same pattern as <c>KineticVfxPresenter</c>).
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Loot Drop VFX Controller")]
    public sealed class LootDropVfxController : MonoBehaviour
    {
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("Lifetime")]
        [Tooltip("How long the cube holds at full brightness between fade-in and fade-out. Task 70 raised the " +
                 "effective visible window from ~1s; ~5s hold (≈6.3s total) gives time to notice without arena clutter.")]
        [SerializeField, Min(0f)] private float _holdSeconds = 5f;
        [SerializeField, Min(0.05f)] private float _fadeInSeconds = 0.3f;
        [SerializeField, Min(0.05f)] private float _fadeOutSeconds = 1f;

        [Header("Cube")]
        [Tooltip("Edge length of the core cube, in metres.")]
        [SerializeField, Min(0.05f)] private float _cubeSize = 0.55f;
        [Tooltip("How high above the death point the cube hovers, in metres.")]
        [SerializeField, Min(0f)] private float _hoverHeight = 0.9f;
        [Tooltip("Size of the soft outer glow cube relative to the core (1 = same size).")]
        [SerializeField, Min(1f)] private float _glowScale = 1.7f;
        [SerializeField, Range(0f, 1f)] private float _glowAlpha = 0.25f;

        [Header("Idle animation")]
        [SerializeField] private float _spinDegreesPerSecond = 80f;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.12f;
        [SerializeField, Min(0f)] private float _bobFrequency = 1.1f;
        [Tooltip("Gentle scale pulse depth (0 = none).")]
        [SerializeField, Range(0f, 0.5f)] private float _pulseDepth = 0.08f;
        [SerializeField, Min(0f)] private float _pulseFrequency = 1.6f;

        private EventBus _events;
        private Mesh _cubeMesh;
        private Material _sharedMaterial;
        private readonly List<Marker> _pool = new List<Marker>();

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogWarning("[LootDropVfxController] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }

            _cubeMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            _sharedMaterial = CreateUnlitMaterial();
            _events = _bootstrap.Session.Events;
            _events.Subscribe<GearDroppedEvent>(OnGearDropped);
        }

        private void OnDestroy()
        {
            if (_events != null) _events.Unsubscribe<GearDroppedEvent>(OnGearDropped);
        }

        private void OnGearDropped(GearDroppedEvent evt)
        {
            if (evt.Item == null) return;
            var marker = GetMarker();
            var spec = new Marker.Spec
            {
                Color = RarityPalette.Color(evt.Item.Rarity),
                CubeSize = _cubeSize,
                HoverHeight = _hoverHeight,
                GlowScale = _glowScale,
                GlowAlpha = _glowAlpha,
                FadeIn = _fadeInSeconds,
                Hold = _holdSeconds,
                FadeOut = _fadeOutSeconds,
                SpinDps = _spinDegreesPerSecond,
                BobAmp = _bobAmplitude,
                BobFreq = _bobFrequency,
                PulseDepth = _pulseDepth,
                PulseFreq = _pulseFrequency,
            };
            marker.Show(evt.DropPosition, spec);
        }

        private void Update()
        {
            // Normal deltaTime: the project pauses via PauseState (not timeScale), so an in-flight marker simply
            // finishes its life after the run ends — harmless, like the other runtime VFX presenters.
            float dt = Time.deltaTime;
            for (int i = 0; i < _pool.Count; i++) _pool[i].Tick(dt);
        }

        // --- marker pool ----------------------------------------------------------------------------

        private Marker GetMarker()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].Active) return _pool[i];

            var marker = new Marker(transform, _cubeMesh, _sharedMaterial);
            _pool.Add(marker);
            return marker;
        }

        // URP-safe unlit material that honours a per-instance _Color (same shader chain as the ability presenters,
        // which use Sprites/Default — alpha-blended + unlit, so the cube reads as "glowing" against the lit scene
        // and can fade via colour alpha).
        private static Material CreateUnlitMaterial()
        {
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            temp.SetActive(false);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }

        /// <summary>One reusable glowing-cube marker: a spinning root with a bright core cube + a larger soft-glow
        /// cube, both tinted via per-renderer MaterialPropertyBlocks. Owned by the controller, never the enemy.</summary>
        private sealed class Marker
        {
            public struct Spec
            {
                public Color Color;
                public float CubeSize, HoverHeight, GlowScale, GlowAlpha;
                public float FadeIn, Hold, FadeOut;
                public float SpinDps, BobAmp, BobFreq, PulseDepth, PulseFreq;
            }

            private static readonly int ColorId = Shader.PropertyToID("_Color");

            private readonly Transform _root;
            private readonly GameObject _rootGo;
            private readonly MeshRenderer _core;
            private readonly MeshRenderer _glow;
            private readonly Transform _coreTr;
            private readonly Transform _glowTr;
            private readonly MaterialPropertyBlock _coreMpb = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock _glowMpb = new MaterialPropertyBlock();

            private Spec _spec;
            private Vector3 _basePos;
            private float _age;
            private float _life;
            private float _phase;
            private float _spinAngle;

            public bool Active { get; private set; }

            public Marker(Transform parent, Mesh cubeMesh, Material material)
            {
                _rootGo = new GameObject("DropCube");
                _root = _rootGo.transform;
                _root.SetParent(parent, false);

                _core = CreateCube("Core", _root, cubeMesh, material, out _coreTr);
                _glow = CreateCube("Glow", _root, cubeMesh, material, out _glowTr);
                _rootGo.SetActive(false);
            }

            private static MeshRenderer CreateCube(string name, Transform parent, Mesh mesh, Material material,
                out Transform tr)
            {
                var go = new GameObject(name);
                tr = go.transform;
                tr.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                return mr;
            }

            public void Show(Vector3 groundPos, Spec spec)
            {
                _spec = spec;
                _basePos = groundPos + Vector3.up * spec.HoverHeight;
                _age = 0f;
                _life = spec.FadeIn + spec.Hold + spec.FadeOut;
                _phase = Random.value * Mathf.PI * 2f;     // de-sync overlapping drops
                _spinAngle = Random.value * 360f;
                Active = true;

                _coreTr.localScale = Vector3.one * spec.CubeSize;
                _glowTr.localScale = Vector3.one * spec.CubeSize * spec.GlowScale;

                // Place it immediately so a reused marker never renders one frame at its previous spot.
                _root.position = _basePos;
                _root.localRotation = Quaternion.AngleAxis(_spinAngle, new Vector3(0.25f, 1f, 0.15f).normalized);

                _rootGo.SetActive(true);
                Apply(0f);
            }

            public void Tick(float dt)
            {
                if (!Active) return;

                _age += dt;
                if (_age >= _life)
                {
                    _rootGo.SetActive(false);
                    Active = false;
                    return;
                }

                // Idle motion: spin around a tilted axis, bob vertically, gentle scale pulse.
                _spinAngle += _spec.SpinDps * dt;
                _root.localRotation = Quaternion.AngleAxis(_spinAngle, new Vector3(0.25f, 1f, 0.15f).normalized);

                float t = _age + _phase;
                float bob = Mathf.Sin(t * _spec.BobFreq * Mathf.PI * 2f) * _spec.BobAmp;
                _root.position = _basePos + Vector3.up * bob;

                float pulse = 1f + Mathf.Sin(t * _spec.PulseFreq * Mathf.PI * 2f) * _spec.PulseDepth;
                _coreTr.localScale = Vector3.one * (_spec.CubeSize * pulse);
                _glowTr.localScale = Vector3.one * (_spec.CubeSize * _spec.GlowScale * pulse);

                Apply(FadeAlpha());
            }

            private float FadeAlpha()
            {
                if (_age < _spec.FadeIn) return _spec.FadeIn > 0f ? _age / _spec.FadeIn : 1f;
                float fadeOutStart = _spec.FadeIn + _spec.Hold;
                if (_age < fadeOutStart) return 1f;
                float k = _spec.FadeOut > 0f ? (_age - fadeOutStart) / _spec.FadeOut : 1f;
                return Mathf.Clamp01(1f - k);
            }

            private void Apply(float alpha)
            {
                var core = _spec.Color; core.a = alpha;
                _coreMpb.SetColor(ColorId, core);
                _core.SetPropertyBlock(_coreMpb);

                var glow = _spec.Color; glow.a = alpha * _spec.GlowAlpha;
                _glowMpb.SetColor(ColorId, glow);
                _glow.SetPropertyBlock(_glowMpb);
            }
        }
    }
}
