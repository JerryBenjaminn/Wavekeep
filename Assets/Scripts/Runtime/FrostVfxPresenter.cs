using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 45: renders Frost Warden's ability VFX, reusing Task 44's blue/white crystallization look (the
    /// shared <c>Wavekeep/FrostFx</c> shader). Implements <see cref="IAbilityFeedback"/> so it is driven from
    /// the SAME execution points as the gameplay (via <see cref="CompositeAbilityFeedback"/> alongside the
    /// generic Task 08 presenter) — there is no parallel ability-trigger path, and every effect is sized/timed
    /// from the ACTUAL resolved values passed in (blast radius, Frozen Ground radius/duration, the zone band
    /// and its real pulse cadence), never hardcoded gameplay numbers.
    ///
    /// Self-contained: all meshes/materials are built at runtime (no scene/prefab wiring), pooled and reused so
    /// repeated casts don't allocate or leak. The generic beam/ring methods no-op here (the Task 08 presenter
    /// owns those). Effects are animated in <see cref="Update"/> using the same deltaTime convention as the
    /// existing presenter; the project pauses via PauseState, so no new casts fire while paused.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Frost VFX Presenter")]
    public sealed class FrostVfxPresenter : MonoBehaviour, IAbilityFeedback
    {
        [Header("Presentation (visual only — not gameplay values)")]
        [Tooltip("Visual width (X) of the full-arena Frost Zone band; the band is gameplay-unbounded in X.")]
        [SerializeField] private float _zoneWidth = 28f;
        [SerializeField] private float _groundY = 0.06f;     // decal/band height above the floor
        [SerializeField] private float _projectileSpeed = 45f;
        [SerializeField] private float _projectileMinFlight = 0.04f;
        [SerializeField] private float _projectileMaxFlight = 0.25f;
        [SerializeField] private float _burstDuration = 0.4f;
        [SerializeField] private float _patchFadeOut = 0.5f;

        // Shared materials (one each, varied per effect); per-instance _Alpha/_Emission ride a MaterialPropertyBlock.
        private Material _burstMat;
        private Material _patchMat;
        private Material _zoneMat;
        private Material _projectileMat;
        private Material _trailMat;
        private bool _ready;

        private static int _idAlpha, _idEmission;

        private Mesh _sphereMesh;
        private Mesh _diskMesh;
        private Mesh _bandMesh;

        private readonly List<Burst> _bursts = new List<Burst>();
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly List<Patch> _patches = new List<Patch>();
        private readonly List<ZoneVisual> _zones = new List<ZoneVisual>();

        private void Awake()
        {
            var shader = Resources.Load<Shader>("FrostFx");
            if (shader == null)
            {
                Debug.LogWarning("[FrostVfxPresenter] 'FrostFx' shader not found in Resources; ability VFX disabled.");
                return;
            }

            _idAlpha = Shader.PropertyToID("_Alpha");
            _idEmission = Shader.PropertyToID("_Emission");

            // Expanding impact shell — crack-heavy, bright, strong rim.
            _burstMat = MakeFx(shader, new Color(0.82f, 0.94f, 1f), fill: 0.16f, crackStrength: 1.3f,
                voronoi: 6f, crackWidth: 0.22f, fresnelBoost: 1.0f, mist: 0f);
            // Glowing projectile core — mostly solid, small.
            _projectileMat = MakeFx(shader, new Color(0.85f, 0.96f, 1f), fill: 0.85f, crackStrength: 0.6f,
                voronoi: 12f, crackWidth: 0.2f, fresnelBoost: 0.8f, mist: 0f);
            // Frozen Ground decal — visible crack pattern, low glow, distinct from the burst.
            _patchMat = MakeFx(shader, new Color(0.6f, 0.85f, 1f), fill: 0.32f, crackStrength: 1.1f,
                voronoi: 9f, crackWidth: 0.16f, fresnelBoost: 0.2f, mist: 0f);
            // Frost Zone ambient — subtle scrolling mist (Task 44 Slow-tier spirit), light coverage.
            _zoneMat = MakeFx(shader, new Color(0.62f, 0.85f, 1f), fill: 0.10f, crackStrength: 0.35f,
                voronoi: 5f, crackWidth: 0.2f, fresnelBoost: 0.15f, mist: 0.85f);

            _trailMat = CreateTrailMaterial(new Color(0.7f, 0.92f, 1f, 1f));

            _sphereMesh = GetPrimitiveMesh(PrimitiveType.Sphere);
            _diskMesh = GetPrimitiveMesh(PrimitiveType.Cylinder); // unit-diameter, height 2; flattened on use
            _bandMesh = GetPrimitiveMesh(PrimitiveType.Cube);

            _ready = true;
        }

        // --- IAbilityFeedback: generic beam/ring handled by the Task 08 presenter (no-op here). ---
        public void OnSingleTargetHit(Vector3 from, Vector3 to) { }
        public void OnAreaOfEffect(Vector3 center, float radius) { }

        // Task 46: Bolt Striker electrical VFX is handled by LightningVfxPresenter (no-op here).
        public void OnLightningStrike(Vector3 from, Vector3 to, LightningStrikeFlags flags) { }
        public void OnChainJump(Vector3 from, Vector3 to) { }
        public void OnArmorBreak(Transform target, float duration) { }
        public void OnVulnerability(Transform target, float duration) { }

        // Task 47: apex / combo apex VFX is handled by ApexVfxPresenter (no-op here).
        public void OnApexImpact(Vector3 center, float radius, ApexVfxStyle style) { }
        public void OnComboFrozenLightning(Vector3 center) { }

        // Task 51: Pyromancer fire VFX is handled by FireVfxPresenter (no-op here).
        public void OnFireballImpact(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnCombustion(Vector3 center, float radius) { }
        public void OnSpreadingFlame(Vector3 from, Vector3 to) { }
        public IFireZoneVisual BeginFireWall(float bandMinZ, float bandMaxZ) => null;

        // Task 52: Marksman kinetic VFX is handled by KineticVfxPresenter (no-op here).
        public void OnTracer(Vector3 from, Vector3 to, float intensity, bool sustained) { }
        public void OnPierceImpact(Vector3 point) { }
        public void OnArmorShred(Transform target, int stacks, int maxStacks, float duration) { }
        public void OnMinigunSpinUp(Vector3 at, float intensity) { }

        public void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius)
        {
            if (!_ready) return;

            float dist = Vector3.Distance(from, to);
            float flight = Mathf.Clamp(dist / Mathf.Max(1f, _projectileSpeed), _projectileMinFlight, _projectileMaxFlight);

            var p = AcquireProjectile();
            p.From = from;
            p.To = to;
            p.Age = 0f;
            p.Flight = flight;
            p.BurstRadius = Mathf.Max(0.25f, burstRadius);
            p.Fx.Transform.position = from;
            p.Fx.Transform.localScale = Vector3.one * 0.45f;
            p.Fx.SetAlphaEmission(1f, 1.1f);
            p.Fx.Activate(true);
            p.Trail.Clear();        // wipe the streak from a previous flight (after re-activating + repositioning)
            p.Trail.emitting = true;
        }

        public void OnGroundPatch(Vector3 center, float radius, float duration)
        {
            if (!_ready || radius <= 0f || duration <= 0f) return;

            var patch = AcquirePatch();
            patch.Age = 0f;
            patch.Life = duration;
            patch.Fx.Transform.position = new Vector3(center.x, _groundY, center.z);
            // Cylinder: unit diameter on X/Z → scale by 2*radius; flattened on Y.
            patch.Fx.Transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            patch.Fx.SetAlphaEmission(0.8f, 0.15f);
            patch.Fx.Activate(true);
        }

        public IZoneVisual BeginZone(float bandMinZ, float bandMaxZ)
        {
            if (!_ready) return null;

            float depth = Mathf.Max(0.2f, bandMaxZ - bandMinZ);
            float centerZ = (bandMinZ + bandMaxZ) * 0.5f;

            var zone = AcquireZone();
            zone.Disposing = false;
            zone.PulseFlash = 0f;
            zone.Activation = 1f; // establish-flash on cast (reveals the full band extent)
            zone.Fx.Transform.position = new Vector3(0f, _groundY, centerZ);
            zone.Fx.Transform.localScale = new Vector3(_zoneWidth, 0.05f, depth);
            zone.Fx.SetAlphaEmission(0.6f, 1.2f);
            zone.Fx.Activate(true);
            return zone;
        }

        private void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;

            TickProjectiles(dt);
            TickBursts(dt);
            TickPatches(dt);
            TickZones(dt);
        }

        private void TickProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                p.Age += dt;
                float t = Mathf.Clamp01(p.Age / p.Flight);
                // Travel with a slight upward arc so it reads as a thrown ice-ball, not a flat slide.
                Vector3 pos = Vector3.Lerp(p.From, p.To, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 0.6f;
                p.Fx.Transform.position = pos;

                if (t >= 1f)
                {
                    SpawnBurst(p.To, p.BurstRadius);
                    p.Trail.emitting = false;
                    p.Fx.Activate(false);
                    _projectiles.RemoveAt(i);
                    _projectileFree.Push(p);
                }
            }
        }

        private void SpawnBurst(Vector3 center, float radius)
        {
            var b = AcquireBurst();
            b.Age = 0f;
            b.Life = _burstDuration;
            b.Radius = radius;
            b.Fx.Transform.position = center;
            b.Fx.Transform.localScale = Vector3.one * 0.1f;
            b.Fx.SetAlphaEmission(0.95f, 1.6f);
            b.Fx.Activate(true);
        }

        private void TickBursts(float dt)
        {
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i];
                b.Age += dt;
                float t = Mathf.Clamp01(b.Age / b.Life);
                // Ease-out expansion to the full (diameter = 2×radius) shell; fade alpha + emission as it grows.
                float scale = Mathf.Lerp(0.1f, b.Radius * 2f, 1f - (1f - t) * (1f - t));
                b.Fx.Transform.localScale = Vector3.one * scale;
                b.Fx.SetAlphaEmission(0.95f * (1f - t), 1.6f * (1f - t));

                if (t >= 1f)
                {
                    b.Fx.Activate(false);
                    _bursts.RemoveAt(i);
                    _burstFree.Push(b);
                }
            }
        }

        private void TickPatches(float dt)
        {
            for (int i = _patches.Count - 1; i >= 0; i--)
            {
                var p = _patches[i];
                p.Age += dt;
                // Hold full, then fade over the last _patchFadeOut seconds of its life.
                float remaining = p.Life - p.Age;
                float alpha = remaining >= _patchFadeOut ? 0.8f : Mathf.Lerp(0f, 0.8f, remaining / _patchFadeOut);
                p.Fx.SetAlphaEmission(Mathf.Max(0f, alpha), 0.15f);

                if (p.Age >= p.Life)
                {
                    p.Fx.Activate(false);
                    _patches.RemoveAt(i);
                    _patchFree.Push(p);
                }
            }
        }

        private void TickZones(float dt)
        {
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var z = _zones[i];
                if (z.Activation > 0f) z.Activation = Mathf.MoveTowards(z.Activation, 0f, dt / 0.4f);
                if (z.PulseFlash > 0f) z.PulseFlash = Mathf.MoveTowards(z.PulseFlash, 0f, dt / 0.45f);

                float ambientAlpha = 0.6f;
                if (z.Disposing)
                {
                    z.FadeOut = Mathf.MoveTowards(z.FadeOut, 0f, dt / 0.4f);
                    ambientAlpha *= z.FadeOut;
                }

                // Activation flash establishes the band; pulse flash rides the real damage tick.
                float emission = z.Activation * 1.2f + z.PulseFlash * 1.0f;
                z.Fx.SetAlphaEmission(ambientAlpha, emission);

                if (z.Disposing && z.FadeOut <= 0f)
                {
                    z.Fx.Activate(false);
                    _zones.RemoveAt(i);
                    _zoneFree.Push(z);
                }
            }
        }

        // --- Pools -------------------------------------------------------------------------------

        private readonly Stack<Burst> _burstFree = new Stack<Burst>();
        private readonly Stack<Projectile> _projectileFree = new Stack<Projectile>();
        private readonly Stack<Patch> _patchFree = new Stack<Patch>();
        private readonly Stack<ZoneVisual> _zoneFree = new Stack<ZoneVisual>();

        private Burst AcquireBurst()
        {
            var b = _burstFree.Count > 0 ? _burstFree.Pop() : new Burst { Fx = NewFx("FrostBurst", _sphereMesh, _burstMat) };
            _bursts.Add(b);
            return b;
        }

        private Projectile AcquireProjectile()
        {
            Projectile p;
            if (_projectileFree.Count > 0) p = _projectileFree.Pop();
            else
            {
                p = new Projectile { Fx = NewFx("FrostBolt", _sphereMesh, _projectileMat) };
                p.Trail = p.Fx.GameObject.AddComponent<TrailRenderer>();
                p.Trail.time = 0.18f;
                p.Trail.startWidth = 0.35f;
                p.Trail.endWidth = 0.02f;
                p.Trail.material = _trailMat;
                p.Trail.startColor = new Color(0.8f, 0.95f, 1f, 0.9f);
                p.Trail.endColor = new Color(0.6f, 0.85f, 1f, 0f);
                p.Trail.shadowCastingMode = ShadowCastingMode.Off;
                p.Trail.receiveShadows = false;
                p.Trail.emitting = false;
            }
            _projectiles.Add(p);
            return p;
        }

        private Patch AcquirePatch()
        {
            var p = _patchFree.Count > 0 ? _patchFree.Pop() : new Patch { Fx = NewFx("FrozenGround", _diskMesh, _patchMat) };
            _patches.Add(p);
            return p;
        }

        private ZoneVisual AcquireZone()
        {
            var z = _zoneFree.Count > 0 ? _zoneFree.Pop() : new ZoneVisual { Fx = NewFx("FrostZoneBand", _bandMesh, _zoneMat) };
            z.FadeOut = 1f;
            _zones.Add(z);
            return z;
        }

        // --- Construction helpers ----------------------------------------------------------------

        private Material MakeFx(Shader shader, Color color, float fill, float crackStrength, float voronoi,
            float crackWidth, float fresnelBoost, float mist)
        {
            var m = new Material(shader);
            m.SetColor("_Color", color);
            m.SetFloat("_FillBase", fill);
            m.SetFloat("_CrackStrength", crackStrength);
            m.SetFloat("_VoronoiScale", voronoi);
            m.SetFloat("_CrackWidth", crackWidth);
            m.SetFloat("_FresnelBoost", fresnelBoost);
            m.SetFloat("_MistAmount", mist);
            return m;
        }

        private static Material CreateTrailMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            return new Material(shader) { color = color };
        }

        private FxInstance NewFx(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new FxInstance(go, mr);
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            temp.SetActive(false); // hide for the frame before Destroy resolves
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh; // built-in asset, survives temp's destruction
            Destroy(temp);
            return mesh;
        }

        // --- Small data holders ------------------------------------------------------------------

        // Wraps one pooled renderer GO + its per-instance MaterialPropertyBlock.
        private sealed class FxInstance
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            private readonly MeshRenderer _renderer;
            private readonly MaterialPropertyBlock _mpb;

            public FxInstance(GameObject go, MeshRenderer renderer)
            {
                GameObject = go;
                Transform = go.transform;
                _renderer = renderer;
                _mpb = new MaterialPropertyBlock();
            }

            public void SetAlphaEmission(float alpha, float emission)
            {
                _mpb.SetFloat(_idAlpha, alpha);
                _mpb.SetFloat(_idEmission, emission);
                _renderer.SetPropertyBlock(_mpb);
            }

            public void Activate(bool on)
            {
                if (GameObject.activeSelf != on) GameObject.SetActive(on);
            }
        }

        private sealed class Burst { public FxInstance Fx; public float Age, Life, Radius; }

        private sealed class Projectile
        {
            public FxInstance Fx;
            public TrailRenderer Trail;
            public Vector3 From, To;
            public float Age, Flight, BurstRadius;
        }

        private sealed class Patch { public FxInstance Fx; public float Age, Life; }

        // The persistent Frost Zone band visual + its IZoneVisual handle (driven by the GroundZone).
        private sealed class ZoneVisual : IZoneVisual
        {
            public FxInstance Fx;
            public float Activation, PulseFlash, FadeOut;
            public bool Disposing;

            public void Pulse() => PulseFlash = 1f;
            public void Dispose() => Disposing = true;
        }
    }
}
