using UnityEngine;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Placeholder visual feedback for ability execution (Task 08 Part A, CLAUDE.md §6 placeholder-tier).
    /// Implements <see cref="IAbilityFeedback"/>; <c>AbilityRuntime</c> calls it at the exact moment it
    /// resolves a target/radius, so what's drawn is always the real resolved hit (no separate targeting).
    ///
    /// Two brief <see cref="LineRenderer"/> flashes, built at runtime so no scene/prefab wiring is
    /// needed: a straight beam (single-target, caster→target) and a flat ring (AoE, drawn at the actual
    /// radius around the caster, on the enemies' Y-plane so the boundary lines up exactly with the
    /// sphere-overlap test). The ring makes it directly visible whether edge enemies fall inside or
    /// outside the AoE — the tool for the Task 06 range diagnosis.
    ///
    /// Distinguishes by TARGETING TYPE (beam vs ring), which is the diagnostically important split;
    /// it does not separately style basic vs ultimate, since the resolution point in the role-agnostic
    /// <c>AbilityRuntime</c> doesn't know which slot fired (kept out of scope to avoid leaking role into
    /// the logic layer). Flash durations/widths/colours are serialized presentation values, not gameplay
    /// numbers, so tuning them needs no SO.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Ability Indicator Presenter")]
    public sealed class AbilityIndicatorPresenter : MonoBehaviour, IAbilityFeedback
    {
        [Header("Flash")]
        [SerializeField, Min(0.02f)] private float _flashDuration = 0.12f;
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.15f;

        [Header("Single-target beam")]
        [SerializeField] private Color _beamColor = new Color(0.4f, 0.9f, 1f);

        [Header("AoE ring")]
        [SerializeField] private Color _ringColor = new Color(1f, 0.55f, 0.1f);
        [SerializeField, Range(12, 96)] private int _ringSegments = 48;

        private LineRenderer _beam;
        private LineRenderer _ring;
        private float _beamTimer;
        private float _ringTimer;

        private void Awake()
        {
            _beam = CreateLine("BasicBeam", _beamColor, 2, loop: false);
            _ring = CreateLine("AoERing", _ringColor, _ringSegments, loop: true);
        }

        private LineRenderer CreateLine(string name, Color color, int positionCount, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = positionCount;
            lr.widthMultiplier = _lineWidth;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            lr.material = CreateUnlitMaterial();
            lr.startColor = color;
            lr.endColor = color;
            lr.enabled = false;
            return lr;
        }

        // URP-safe unlit material for runtime lines. Sprites/Default honours LineRenderer vertex colours
        // and renders under URP; fall back through a couple of always-present unlit shaders if stripped.
        private static Material CreateUnlitMaterial()
        {
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }

        public void OnSingleTargetHit(Vector3 from, Vector3 to)
        {
            if (_beam == null) return;
            _beam.SetPosition(0, from);
            _beam.SetPosition(1, to);
            _beam.enabled = true;
            _beamTimer = _flashDuration;
        }

        public void OnAreaOfEffect(Vector3 center, float radius)
        {
            if (_ring == null) return;

            // Draw the ring in the horizontal plane through the caster — the exact plane the enemies
            // (same Y) are tested against — so the circle boundary equals the overlap radius.
            for (int i = 0; i < _ringSegments; i++)
            {
                float angle = (i / (float)_ringSegments) * Mathf.PI * 2f;
                var point = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y,
                    center.z + Mathf.Sin(angle) * radius);
                _ring.SetPosition(i, point);
            }
            _ring.enabled = true;
            _ringTimer = _flashDuration;
        }

        // Task 45: frost-styled ability VFX is rendered by FrostVfxPresenter, not this generic diagnostic
        // beam/ring presenter — these are no-ops here so both can coexist on the hero via the composite sink.
        public void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnGroundPatch(Vector3 center, float radius, float duration) { }
        public Wavekeep.Abilities.IZoneVisual BeginZone(float bandMinZ, float bandMaxZ) => null;

        // Task 46: Bolt Striker electrical VFX is rendered by LightningVfxPresenter — no-ops here.
        public void OnLightningStrike(Vector3 from, Vector3 to, Wavekeep.Abilities.LightningStrikeFlags flags) { }
        public void OnChainJump(Vector3 from, Vector3 to) { }
        public void OnArmorBreak(Transform target, float duration) { }
        public void OnVulnerability(Transform target, float duration) { }

        // Task 47: apex / combo apex VFX is rendered by ApexVfxPresenter — no-ops here.
        public void OnApexImpact(Vector3 center, float radius, Wavekeep.Abilities.ApexVfxStyle style) { }
        public void OnComboFrozenLightning(Vector3 center) { }

        // Task 51: Pyromancer fire VFX is rendered by FireVfxPresenter — no-ops here.
        public void OnFireballImpact(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnCombustion(Vector3 center, float radius) { }
        public void OnSpreadingFlame(Vector3 from, Vector3 to) { }
        public Wavekeep.Abilities.IFireZoneVisual BeginFireWall(float bandMinZ, float bandMaxZ) => null;

        // Task 52: Marksman kinetic VFX is rendered by KineticVfxPresenter — no-ops here.
        public void OnTracer(Vector3 from, Vector3 to, float intensity, bool sustained) { }
        public void OnPierceImpact(Vector3 point) { }
        public void OnArmorShred(Transform target, int stacks, int maxStacks, float duration) { }
        public void OnMinigunSpinUp(Vector3 at, float intensity) { }

        private void Update()
        {
            // Uses normal deltaTime; the project pauses via PauseState (not timeScale), so a frozen run
            // simply stops firing new flashes — any in-flight flash finishes fading out, which is fine.
            if (_beamTimer > 0f)
            {
                _beamTimer -= Time.deltaTime;
                if (_beamTimer <= 0f) _beam.enabled = false;
            }

            if (_ringTimer > 0f)
            {
                _ringTimer -= Time.deltaTime;
                if (_ringTimer <= 0f) _ring.enabled = false;
            }
        }
    }
}
