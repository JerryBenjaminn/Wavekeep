using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 51: renders the Pyromancer's ability VFX (warm red/orange/yellow), the fire counterpart to Task 45's
    /// <see cref="FrostVfxPresenter"/>. Implements <see cref="IAbilityFeedback"/> so it is driven from the SAME
    /// execution / reaction points as the gameplay (joined to the hero's <see cref="CompositeAbilityFeedback"/>
    /// alongside the other presenters) — no parallel trigger path, and every effect is sized/timed from the
    /// ACTUAL resolved values passed in (the Combustion blast radius for the current tier, the Firewall band, the
    /// spread origin/target), never hardcoded gameplay numbers.
    ///
    /// Covers: the Fireball projectile + impact burst, the Combustion detonation burst, the Spreading Flame ember
    /// streak, and the persistent Firewall wall-of-fire band (with Inferno Surge flares ridden off the zone's real
    /// pulse, and Wildfire Spread cooling-ember after-patches). Self-contained — all meshes/materials are built at
    /// runtime, pooled and reused, animated in <see cref="Update"/>; the generic/frost/lightning/apex calls no-op
    /// here. Inert (no allocation) for heroes whose abilities never call the fire methods.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Fire VFX Presenter")]
    public sealed class FireVfxPresenter : MonoBehaviour, IAbilityFeedback
    {
        [Header("Presentation (visual only — not gameplay values)")]
        [Tooltip("Visual width (X) of the full-arena Firewall band; the band is gameplay-unbounded in X.")]
        [SerializeField] private float _wallWidth = 28f;
        [SerializeField] private float _wallHeight = 2.6f;     // a vertical WALL of flame, not a flat ground patch
        [SerializeField] private float _groundY = 0.06f;
        [SerializeField] private float _projectileSpeed = 50f;
        [SerializeField] private float _projectileMinFlight = 0.04f;
        [SerializeField] private float _projectileMaxFlight = 0.22f;
        [SerializeField] private float _burstDuration = 0.4f;
        [SerializeField] private float _sparkDuration = 0.22f;

        private static int _idAlpha, _idEmission;

        private Material _projectileMat;
        private Material _burstMat;
        private Material _wallMat;
        private Material _patchMat;
        private Material _trailMat;
        private Material _lineMat;
        private bool _ready;

        private Mesh _sphereMesh;
        private Mesh _diskMesh;
        private Mesh _bandMesh;

        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly List<Burst> _bursts = new List<Burst>();
        private readonly List<Patch> _patches = new List<Patch>();
        private readonly List<WallVisual> _walls = new List<WallVisual>();
        private readonly List<Spark> _sparks = new List<Spark>();

        private readonly Stack<Projectile> _projectileFree = new Stack<Projectile>();
        private readonly Stack<Burst> _burstFree = new Stack<Burst>();
        private readonly Stack<Patch> _patchFree = new Stack<Patch>();
        private readonly Stack<WallVisual> _wallFree = new Stack<WallVisual>();
        private readonly Stack<Spark> _sparkFree = new Stack<Spark>();

        private void Awake()
        {
            var shader = Resources.Load<Shader>("FireFx");
            if (shader == null)
            {
                Debug.LogWarning("[FireVfxPresenter] 'FireFx' shader not found in Resources; fire ability VFX disabled.");
                return;
            }

            _idAlpha = Shader.PropertyToID("_Alpha");
            _idEmission = Shader.PropertyToID("_Emission");

            // Molten projectile core — mostly solid, bright, small embers.
            _projectileMat = MakeFx(shader, new Color(1f, 0.5f, 0.1f), new Color(1f, 0.9f, 0.45f),
                fill: 0.85f, noise: 5f, flameSpeed: 2.2f, fresnelBoost: 0.7f, ember: 0.5f, softEdge: 0.3f);
            // Expanding impact / combustion shell — flame-heavy, bright rim.
            _burstMat = MakeFx(shader, new Color(1f, 0.42f, 0.06f), new Color(1f, 0.85f, 0.35f),
                fill: 0.2f, noise: 4f, flameSpeed: 2.6f, fresnelBoost: 1f, ember: 0.5f, softEdge: 0.55f);
            // Firewall band — a tall, full wall of flame with rich rising fire + embers.
            _wallMat = MakeFx(shader, new Color(1f, 0.38f, 0.05f), new Color(1f, 0.82f, 0.32f),
                fill: 0.35f, noise: 3f, flameSpeed: 1.8f, fresnelBoost: 0.5f, ember: 0.6f, softEdge: 0.5f);
            // Wildfire cooling patch — dim, low embers, NO full flame (a smoldering after-effect).
            _patchMat = MakeFx(shader, new Color(0.6f, 0.18f, 0.04f), new Color(0.95f, 0.5f, 0.15f),
                fill: 0.18f, noise: 5f, flameSpeed: 0.6f, fresnelBoost: 0.15f, ember: 0.7f, softEdge: 0.7f);

            _trailMat = CreateUnlitMaterial(new Color(1f, 0.55f, 0.15f, 1f));
            _lineMat = CreateUnlitMaterial(Color.white);

            _sphereMesh = GetPrimitiveMesh(PrimitiveType.Sphere);
            _diskMesh = GetPrimitiveMesh(PrimitiveType.Cylinder);
            _bandMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            _ready = true;
        }

        // --- IAbilityFeedback: everything except the fire calls belongs to other presenters (no-op). ---
        public void OnSingleTargetHit(Vector3 from, Vector3 to) { }
        public void OnAreaOfEffect(Vector3 center, float radius) { }
        public void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnGroundPatch(Vector3 center, float radius, float duration) { }
        public IZoneVisual BeginZone(float bandMinZ, float bandMaxZ) => null;
        public void OnLightningStrike(Vector3 from, Vector3 to, LightningStrikeFlags flags) { }
        public void OnChainJump(Vector3 from, Vector3 to) { }
        public void OnArmorBreak(Transform target, float duration) { }
        public void OnVulnerability(Transform target, float duration) { }
        public void OnApexImpact(Vector3 center, float radius, ApexVfxStyle style) { }
        public void OnComboFrozenLightning(Vector3 center) { }

        // Task 52: Marksman kinetic VFX is handled by KineticVfxPresenter (no-op here).
        public void OnTracer(Vector3 from, Vector3 to, float intensity, bool sustained) { }
        public void OnPierceImpact(Vector3 point) { }
        public void OnArmorShred(Transform target, int stacks, int maxStacks, float duration) { }
        public void OnMinigunSpinUp(Vector3 at, float intensity) { }

        // --- Fireball -------------------------------------------------------------------------------

        public void OnFireballImpact(Vector3 from, Vector3 to, float burstRadius)
        {
            if (!_ready) return;

            float dist = Vector3.Distance(from, to);
            float flight = Mathf.Clamp(dist / Mathf.Max(1f, _projectileSpeed), _projectileMinFlight, _projectileMaxFlight);

            var p = AcquireProjectile();
            p.From = from;
            p.To = to;
            p.Age = 0f;
            p.Flight = flight;
            p.BurstRadius = Mathf.Max(0.4f, burstRadius);
            p.Fx.Transform.position = from;
            p.Fx.Transform.localScale = Vector3.one * 0.5f;
            p.Fx.SetAlphaEmission(1f, 1.4f);
            p.Fx.Activate(true);
            p.Trail.Clear();
            p.Trail.emitting = true;
        }

        // --- Combustion / Spreading Flame -----------------------------------------------------------

        public void OnCombustion(Vector3 center, float radius)
        {
            if (!_ready) return;
            // A distinct, brighter burst sized to the ACTUAL Combustion tier radius.
            SpawnBurst(center, Mathf.Max(0.5f, radius), 1f, 1.9f);
        }

        public void OnSpreadingFlame(Vector3 from, Vector3 to)
        {
            if (!_ready) return;

            var s = AcquireSpark();
            s.From = from;
            s.To = to;
            s.Age = 0f;
            s.Line.enabled = true;
            BuildSparkLine(s.Line, from, to, 1f);

            // A small ignite pop at the newly-lit target so the spread reads as "it caught fire".
            SpawnBurst(to, 0.8f, 0.85f, 1.4f);
        }

        // --- Firewall -------------------------------------------------------------------------------

        public IFireZoneVisual BeginFireWall(float bandMinZ, float bandMaxZ)
        {
            if (!_ready) return null;

            float depth = Mathf.Max(0.2f, bandMaxZ - bandMinZ);
            float centerZ = (bandMinZ + bandMaxZ) * 0.5f;

            var wall = AcquireWall();
            wall.Owner = this;
            wall.Disposing = false;
            wall.FadeOut = 1f;
            wall.PulseFlash = 0f;
            wall.Activation = 1f; // establish/sweep flash + vertical grow on cast
            wall.Depth = depth;
            wall.CenterZ = centerZ;
            wall.Fx.Transform.position = new Vector3(0f, _groundY + _wallHeight * 0.5f, centerZ);
            wall.Fx.Transform.localScale = new Vector3(_wallWidth, _wallHeight, depth);
            wall.Fx.SetAlphaEmission(0.7f, 1.4f);
            wall.Fx.Activate(true);
            return wall;
        }

        private void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            TickProjectiles(dt);
            TickBursts(dt);
            TickPatches(dt);
            TickWalls(dt);
            TickSparks(dt);
        }

        private void TickProjectiles(float dt)
        {
            for (int i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                p.Age += dt;
                float t = Mathf.Clamp01(p.Age / p.Flight);
                Vector3 pos = Vector3.Lerp(p.From, p.To, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 0.6f; // slight thrown arc
                p.Fx.Transform.position = pos;

                if (t >= 1f)
                {
                    SpawnBurst(p.To, p.BurstRadius, 0.95f, 1.7f);
                    p.Trail.emitting = false;
                    p.Fx.Activate(false);
                    _projectiles.RemoveAt(i);
                    _projectileFree.Push(p);
                }
            }
        }

        private void SpawnBurst(Vector3 center, float radius, float peakAlpha, float peakEmission)
        {
            var b = AcquireBurst();
            b.Age = 0f;
            b.Life = _burstDuration;
            b.Radius = radius;
            b.PeakAlpha = peakAlpha;
            b.PeakEmission = peakEmission;
            b.Fx.Transform.position = center;
            b.Fx.Transform.localScale = Vector3.one * 0.1f;
            b.Fx.SetAlphaEmission(peakAlpha, peakEmission);
            b.Fx.Activate(true);
        }

        private void TickBursts(float dt)
        {
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i];
                b.Age += dt;
                float t = Mathf.Clamp01(b.Age / b.Life);
                float scale = Mathf.Lerp(0.1f, b.Radius * 2f, 1f - (1f - t) * (1f - t));
                b.Fx.Transform.localScale = Vector3.one * scale;
                b.Fx.SetAlphaEmission(b.PeakAlpha * (1f - t), b.PeakEmission * (1f - t));

                if (t >= 1f)
                {
                    b.Fx.Activate(false);
                    _bursts.RemoveAt(i);
                    _burstFree.Push(b);
                }
            }
        }

        // A cooling Wildfire-Spread patch: holds dim embers, then fades over the last portion of its life.
        private void SpawnCoolingPatch(Vector3 center, float radius, float life)
        {
            if (!_ready || radius <= 0f || life <= 0f) return;
            var p = AcquirePatch();
            p.Age = 0f;
            p.Life = life;
            p.Fx.Transform.position = new Vector3(center.x, _groundY, center.z);
            p.Fx.Transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            p.Fx.SetAlphaEmission(0.45f, 0.3f);
            p.Fx.Activate(true);
        }

        private void TickPatches(float dt)
        {
            const float fade = 0.6f;
            for (int i = _patches.Count - 1; i >= 0; i--)
            {
                var p = _patches[i];
                p.Age += dt;
                float remaining = p.Life - p.Age;
                float alpha = remaining >= fade ? 0.45f : Mathf.Lerp(0f, 0.45f, remaining / fade);
                p.Fx.SetAlphaEmission(Mathf.Max(0f, alpha), 0.3f * Mathf.Clamp01(remaining / Mathf.Max(0.01f, p.Life)));

                if (p.Age >= p.Life)
                {
                    p.Fx.Activate(false);
                    _patches.RemoveAt(i);
                    _patchFree.Push(p);
                }
            }
        }

        private void TickWalls(float dt)
        {
            for (int i = _walls.Count - 1; i >= 0; i--)
            {
                var w = _walls[i];
                if (w.Activation > 0f) w.Activation = Mathf.MoveTowards(w.Activation, 0f, dt / 0.35f);
                if (w.PulseFlash > 0f) w.PulseFlash = Mathf.MoveTowards(w.PulseFlash, 0f, dt / 0.4f);

                // Vertical "sweep up" on establish: the wall grows to full height as the activation flash decays.
                float heightK = Mathf.Lerp(1f, 0.35f, w.Activation); // 0.35→1 height as Activation 1→0
                w.Fx.Transform.localScale = new Vector3(_wallWidth, _wallHeight * heightK, w.Depth);
                w.Fx.Transform.position = new Vector3(0f, _groundY + _wallHeight * heightK * 0.5f, w.CenterZ);

                float ambientAlpha = 0.7f;
                if (w.Disposing)
                {
                    w.FadeOut = Mathf.MoveTowards(w.FadeOut, 0f, dt / 0.5f);
                    ambientAlpha *= w.FadeOut;
                }

                // Steady ambient (the shader animates the flames); activation + Inferno Surge pulse add flare.
                float emission = 1.2f + w.Activation * 1.0f + w.PulseFlash * 1.6f;
                w.Fx.SetAlphaEmission(ambientAlpha, emission * (w.Disposing ? w.FadeOut : 1f));

                if (w.Disposing && w.FadeOut <= 0f)
                {
                    w.Fx.Activate(false);
                    _walls.RemoveAt(i);
                    _wallFree.Push(w);
                }
            }
        }

        private void TickSparks(float dt)
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Age += dt;
                float t = Mathf.Clamp01(s.Age / _sparkDuration);
                BuildSparkLine(s.Line, s.From, s.To, 1f - t);

                if (s.Age >= _sparkDuration)
                {
                    s.Line.enabled = false;
                    _sparks.RemoveAt(i);
                    _sparkFree.Push(s);
                }
            }
        }

        // --- Pools ----------------------------------------------------------------------------------

        private Projectile AcquireProjectile()
        {
            Projectile p;
            if (_projectileFree.Count > 0) p = _projectileFree.Pop();
            else
            {
                p = new Projectile { Fx = NewFx("Fireball", _sphereMesh, _projectileMat) };
                p.Trail = p.Fx.GameObject.AddComponent<TrailRenderer>();
                p.Trail.time = 0.16f;
                p.Trail.startWidth = 0.4f;
                p.Trail.endWidth = 0.02f;
                p.Trail.material = _trailMat;
                p.Trail.startColor = new Color(1f, 0.6f, 0.15f, 0.9f);
                p.Trail.endColor = new Color(0.6f, 0.15f, 0.02f, 0f);
                p.Trail.shadowCastingMode = ShadowCastingMode.Off;
                p.Trail.receiveShadows = false;
                p.Trail.emitting = false;
            }
            _projectiles.Add(p);
            return p;
        }

        private Burst AcquireBurst()
        {
            var b = _burstFree.Count > 0 ? _burstFree.Pop() : new Burst { Fx = NewFx("FireBurst", _sphereMesh, _burstMat) };
            _bursts.Add(b);
            return b;
        }

        private Patch AcquirePatch()
        {
            var p = _patchFree.Count > 0 ? _patchFree.Pop() : new Patch { Fx = NewFx("WildfirePatch", _diskMesh, _patchMat) };
            _patches.Add(p);
            return p;
        }

        private WallVisual AcquireWall()
        {
            var w = _wallFree.Count > 0 ? _wallFree.Pop() : new WallVisual { Fx = NewFx("FirewallBand", _bandMesh, _wallMat) };
            _walls.Add(w);
            return w;
        }

        private Spark AcquireSpark()
        {
            var s = _sparkFree.Count > 0 ? _sparkFree.Pop() : new Spark { Line = NewLine("SpreadEmber") };
            _sparks.Add(s);
            return s;
        }

        // --- Construction helpers -------------------------------------------------------------------

        private Material MakeFx(Shader shader, Color color, Color hot, float fill, float noise, float flameSpeed,
            float fresnelBoost, float ember, float softEdge)
        {
            var m = new Material(shader);
            m.SetColor("_Color", color);
            m.SetColor("_HotColor", hot);
            m.SetFloat("_FillBase", fill);
            m.SetFloat("_NoiseScale", noise);
            m.SetFloat("_FlameSpeed", flameSpeed);
            m.SetFloat("_FresnelBoost", fresnelBoost);
            m.SetFloat("_EmberAmount", ember);
            m.SetFloat("_SoftEdge", softEdge);
            return m;
        }

        private static Material CreateUnlitMaterial(Color color)
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
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new FxInstance(go, mr);
        }

        private LineRenderer NewLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.material = _lineMat;
            lr.widthMultiplier = 0.22f;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        // A short jagged ember streak from→to, brightening to gold at the head; alpha fades via `k`.
        private static void BuildSparkLine(LineRenderer lr, Vector3 from, Vector3 to, float k)
        {
            Vector3 delta = to - from;
            float len = delta.magnitude;
            int segments = Mathf.Clamp(Mathf.RoundToInt(len / 1.2f), 4, 10);
            Vector3 fwd = len > 0.001f ? delta / len : Vector3.forward;
            Vector3 side = Vector3.Cross(fwd, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            float amp = Mathf.Min(len * 0.08f, 0.5f);
            lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(from, to, t);
                p.y += 0.5f; // ride a touch above the ground so it reads against enemies
                if (i != 0 && i != segments) p += side * Random.Range(-amp, amp);
                lr.SetPosition(i, p);
            }
            var c = new Color(1f, 0.6f, 0.15f, Mathf.Clamp01(k));
            lr.startColor = new Color(0.8f, 0.25f, 0.05f, Mathf.Clamp01(k));
            lr.endColor = c;
            lr.widthMultiplier = 0.22f * Mathf.Clamp01(0.4f + k);
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            temp.SetActive(false);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }

        // --- data holders ---------------------------------------------------------------------------

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

            public void Activate(bool on) { if (GameObject.activeSelf != on) GameObject.SetActive(on); }
        }

        private sealed class Projectile
        {
            public FxInstance Fx;
            public TrailRenderer Trail;
            public Vector3 From, To;
            public float Age, Flight, BurstRadius;
        }

        private sealed class Burst { public FxInstance Fx; public float Age, Life, Radius, PeakAlpha, PeakEmission; }
        private sealed class Patch { public FxInstance Fx; public float Age, Life; }
        private sealed class Spark { public LineRenderer Line; public Vector3 From, To; public float Age; }

        // The persistent Firewall band visual + its IFireZoneVisual handle (driven by the GroundZone).
        private sealed class WallVisual : IFireZoneVisual
        {
            public FxInstance Fx;
            public FireVfxPresenter Owner;
            public float Activation, PulseFlash, FadeOut, Depth, CenterZ;
            public bool Disposing;

            public void Pulse() => PulseFlash = 1f;                       // Inferno Surge flare across the wall
            public void Dispose() => Disposing = true;                    // begin fade-out (band expiry)
            public void SpawnCoolingPatch(float x, float z, float radius, float life)
                => Owner?.SpawnCoolingPatch(new Vector3(x, 0f, z), radius, life);
        }
    }
}
