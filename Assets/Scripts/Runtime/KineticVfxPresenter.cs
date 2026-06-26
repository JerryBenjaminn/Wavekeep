using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 52: renders the Marksman's kinetic VFX — warm-white/pale-orange bullet tracers, gray/white metallic
    /// spark bursts, a muzzle flash, the Armor Shredder fracture indicator, and the Minigun spin-up + sustained
    /// heat-shimmer + brass casing ejections. Implements <see cref="IAbilityFeedback"/> and joins the hero's
    /// <see cref="CompositeAbilityFeedback"/>, so it is driven from the SAME shot-execution points as the gameplay;
    /// every COUNT comes straight from the runtime (one tracer per actual shot, one spark per ACTUALLY pierced
    /// enemy, tracer brightness from the current damage tier, density from the current fire rate) — never hardcoded.
    ///
    /// Mechanically the Marksman is "just a bullet", so the flair is density + snappiness + small kinetic details
    /// rather than an elemental shader: tracers/sparks/muzzle/fracture/casings are pooled <see cref="LineRenderer"/>s
    /// and tiny meshes on one shared unlit material. The only shader is the cosmetic <c>HeatHaze</c> (purely visual,
    /// no overheat mechanic). The Armor Shredder indicator is gray fracture cracks whose density scales with stacks
    /// — deliberately distinct from Bolt Striker's gold Piercing Bolt spokes and Pyromancer's orange Burn glow.
    /// Self-contained (built at runtime), pooled, and torn down cleanly so nothing lingers across casts/pooled reuse.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Kinetic VFX Presenter")]
    public sealed class KineticVfxPresenter : MonoBehaviour, IAbilityFeedback
    {
        [Header("Presentation (visual only)")]
        [SerializeField] private float _tracerFlash = 0.07f;   // snappy — reads as instant, not a travel
        [SerializeField] private float _sparkFlash = 0.12f;
        [SerializeField] private float _muzzleFlash = 0.06f;
        [SerializeField] private float _casingLife = 0.7f;
        [SerializeField] private float _indicatorRadius = 0.8f;

        private static readonly Color Tracer = new Color(1f, 0.93f, 0.78f);   // warm white / pale orange
        private static readonly Color MuzzleColor = new Color(1f, 0.96f, 0.75f);
        private static readonly Color SparkColor = new Color(0.88f, 0.9f, 0.95f); // gray/white metallic
        private static readonly Color FractureColor = new Color(0.82f, 0.85f, 0.9f);
        private static readonly Color Brass = new Color(0.82f, 0.62f, 0.22f);

        private Material _lineMat;
        private Material _casingMat;
        private Material _hazeMat;
        private Mesh _cubeMesh;
        private Mesh _quadMesh;
        private bool _ready;
        private Camera _camera;

        private static int _idHazeLevel;

        private readonly List<Tracer3> _tracers = new List<Tracer3>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly List<Casing> _casings = new List<Casing>();
        private readonly List<DebuffIndicator> _shreds = new List<DebuffIndicator>();

        private readonly Stack<Tracer3> _tracerFree = new Stack<Tracer3>();
        private readonly Stack<Spark> _sparkFree = new Stack<Spark>();
        private readonly Stack<Casing> _casingFree = new Stack<Casing>();
        private readonly Stack<DebuffIndicator> _shredFree = new Stack<DebuffIndicator>();

        // Single persistent heat-haze blob (one hero per presenter), driven by the sustained Minigun fire.
        private FxQuad _haze;
        private float _hazeLevel;       // current build-up 0..1
        private float _hazeRefresh;     // >0 while still firing this frame-window; lapses when fire stops
        private float _hazeIntensity;   // damage-tier driven size/strength
        private Vector3 _hazePos;

        private void Awake()
        {
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            _lineMat = new Material(shader);
            _casingMat = new Material(shader) { color = Brass };

            var hazeShader = Resources.Load<Shader>("HeatHaze");
            if (hazeShader != null)
            {
                _idHazeLevel = Shader.PropertyToID("_Level");
                _hazeMat = new Material(hazeShader);
            }
            else
            {
                Debug.LogWarning("[KineticVfxPresenter] 'HeatHaze' shader not found in Resources; heat-shimmer disabled.");
            }

            _cubeMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            _quadMesh = GetPrimitiveMesh(PrimitiveType.Quad);
            _camera = Camera.main;
            _ready = true;
        }

        // --- IAbilityFeedback: everything except the kinetic calls belongs to other presenters (no-op). ---
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
        public void OnFireballImpact(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnCombustion(Vector3 center, float radius) { }
        public void OnSpreadingFlame(Vector3 from, Vector3 to) { }
        public IFireZoneVisual BeginFireWall(float bandMinZ, float bandMaxZ) => null;

        // --- kinetic VFX ----------------------------------------------------------------------------

        public void OnTracer(Vector3 from, Vector3 to, float intensity, bool sustained)
        {
            if (!_ready) return;

            // Brightness/width scale with the current damage tier (Heavy Rounds). Density (shots/sec) scales for
            // free — faster fire rate simply produces more tracer calls.
            float k = Mathf.InverseLerp(0.6f, 2.5f, intensity);
            float width = Mathf.Lerp(0.05f, 0.14f, k);
            float alpha = Mathf.Lerp(0.75f, 1f, k);

            SpawnTracer(from, to, width, alpha);
            SpawnSpark(from, MuzzleColor, 0.5f + 0.3f * k); // muzzle flash at the firing point

            if (sustained)
            {
                EjectCasing(from, to);
                FeedHaze(from, intensity); // sustained Minigun fire builds the heat-shimmer
            }
        }

        public void OnPierceImpact(Vector3 point)
        {
            if (!_ready) return;
            SpawnSpark(point, SparkColor, 0.7f); // one metallic spark burst per actually-pierced enemy
        }

        public void OnMinigunSpinUp(Vector3 at, float intensity)
        {
            if (!_ready) return;
            // A brief ramp-up flash + spark fan at the muzzle, then prime the heat-shimmer for the channel.
            SpawnSpark(at, MuzzleColor, 1.2f);
            SpawnTracer(at, at + Vector3.up * 0.6f, 0.16f, 1f); // tiny vertical "charge" flick
            FeedHaze(at, intensity);
        }

        public void OnArmorShred(Transform target, int stacks, int maxStacks, float duration)
        {
            if (!_ready || target == null || stacks <= 0 || duration <= 0f) return;

            int max = Mathf.Max(1, maxStacks);
            // Refresh in place if this target already has an indicator (rebuild its density to the new stack count).
            for (int i = 0; i < _shreds.Count; i++)
            {
                if (_shreds[i].Target == target)
                {
                    _shreds[i].Remaining = duration;
                    if (_shreds[i].Stacks != stacks)
                    {
                        _shreds[i].Stacks = stacks;
                        BuildFracture(_shreds[i].Line, stacks, max);
                    }
                    return;
                }
            }

            var d = _shredFree.Count > 0 ? _shredFree.Pop() : NewShredIndicator();
            d.Target = target;
            d.Remaining = duration;
            d.Age = 0f;
            d.Stacks = stacks;
            BuildFracture(d.Line, stacks, max);
            d.Root.transform.position = target.position;
            d.Root.SetActive(true);
            _shreds.Add(d);
        }

        private void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            TickTracers(dt);
            TickSparks(dt);
            TickCasings(dt);
            TickShreds(dt);
            TickHaze(dt);
        }

        // --- tracers (straight, instant) ------------------------------------------------------------

        private void SpawnTracer(Vector3 from, Vector3 to, float width, float alpha)
        {
            var t = _tracerFree.Count > 0 ? _tracerFree.Pop() : new Tracer3 { Line = NewLine("Tracer", loop: false) };
            t.Line.positionCount = 2;
            t.Line.SetPosition(0, from);
            t.Line.SetPosition(1, to);
            t.BaseWidth = width;
            t.Line.widthMultiplier = width;
            var c = Tracer; c.a = alpha;
            t.Color = c;
            t.Line.startColor = c;
            t.Line.endColor = new Color(c.r, c.g, c.b, alpha * 0.4f); // fade toward the far end
            t.Age = 0f;
            t.Line.enabled = true;
            _tracers.Add(t);
        }

        private void TickTracers(float dt)
        {
            for (int i = _tracers.Count - 1; i >= 0; i--)
            {
                var t = _tracers[i];
                t.Age += dt;
                float k = 1f - Mathf.Clamp01(t.Age / _tracerFlash);
                t.Line.widthMultiplier = t.BaseWidth * k;
                var c = t.Color; c.a = t.Color.a * k;
                t.Line.startColor = c;
                t.Line.endColor = new Color(c.r, c.g, c.b, c.a * 0.4f);

                if (t.Age >= _tracerFlash)
                {
                    t.Line.enabled = false;
                    _tracers.RemoveAt(i);
                    _tracerFree.Push(t);
                }
            }
        }

        // --- sparks (radial star) -------------------------------------------------------------------

        private void SpawnSpark(Vector3 center, Color color, float scale)
        {
            var s = _sparkFree.Count > 0 ? _sparkFree.Pop() : new Spark { Line = NewLine("Spark", loop: false) };
            s.Center = center;
            s.Color = color;
            s.Scale = scale;
            s.Age = 0f;
            s.Life = _sparkFlash;
            BuildStar(s.Line, center, scale * 0.6f);
            s.Line.startColor = color;
            s.Line.endColor = color;
            s.Line.widthMultiplier = 0.05f;
            s.Line.enabled = true;
            _sparks.Add(s);
        }

        private void TickSparks(float dt)
        {
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Age += dt;
                float t = Mathf.Clamp01(s.Age / s.Life);
                BuildStar(s.Line, s.Center, s.Scale * (0.6f + t * 0.7f)); // expand as it fades
                var c = s.Color; c.a = 1f - t;
                s.Line.startColor = c;
                s.Line.endColor = c;

                if (s.Age >= s.Life)
                {
                    s.Line.enabled = false;
                    _sparks.RemoveAt(i);
                    _sparkFree.Push(s);
                }
            }
        }

        // --- brass casings (tumbling ejected cartridges) --------------------------------------------

        private void EjectCasing(Vector3 muzzle, Vector3 shotTo)
        {
            Vector3 dir = shotTo - muzzle; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward; else dir.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, dir); // eject to the weapon's side

            var c = _casingFree.Count > 0 ? _casingFree.Pop() : NewCasing();
            c.Age = 0f;
            c.Pos = muzzle + Vector3.up * 0.4f;
            c.Vel = right * Random.Range(1.5f, 2.6f) + Vector3.up * Random.Range(1.8f, 2.8f)
                    + dir * Random.Range(-0.3f, 0.3f);
            c.Spin = new Vector3(Random.Range(-720f, 720f), Random.Range(-720f, 720f), Random.Range(-720f, 720f));
            c.Tr.position = c.Pos;
            c.Tr.rotation = Random.rotation;
            c.Go.SetActive(true);
            _casings.Add(c);
        }

        private void TickCasings(float dt)
        {
            for (int i = _casings.Count - 1; i >= 0; i--)
            {
                var c = _casings[i];
                c.Age += dt;
                c.Vel += Vector3.down * 9.8f * dt; // gravity
                c.Pos += c.Vel * dt;
                c.Tr.position = c.Pos;
                c.Tr.Rotate(c.Spin * dt, Space.Self);

                if (c.Age >= _casingLife)
                {
                    c.Go.SetActive(false);
                    _casings.RemoveAt(i);
                    _casingFree.Push(c);
                }
            }
        }

        // --- Armor Shredder fracture indicator ------------------------------------------------------

        private void TickShreds(float dt)
        {
            for (int i = _shreds.Count - 1; i >= 0; i--)
            {
                var d = _shreds[i];
                d.Remaining -= dt;
                d.Age += dt;

                bool targetGone = d.Target == null || !d.Target.gameObject.activeInHierarchy;
                if (d.Remaining <= 0f || targetGone)
                {
                    d.Root.SetActive(false);
                    d.Target = null;
                    _shreds.RemoveAt(i);
                    _shredFree.Push(d);
                    continue;
                }

                d.Root.transform.position = d.Target.position;
                // Subtle steady shimmer; brighter with more stacks (fracture density already conveys the count).
                float a = 0.55f + 0.2f * Mathf.Abs(Mathf.Sin(d.Age * 4f));
                var c = FractureColor; c.a = a;
                d.Line.startColor = c;
                d.Line.endColor = c;
            }
        }

        // --- heat shimmer ---------------------------------------------------------------------------

        private void FeedHaze(Vector3 at, float intensity)
        {
            if (_hazeMat == null) return;
            _hazeRefresh = 0.4f;                 // refreshed by each sustained shot; lapses when fire stops
            _hazePos = at;
            _hazeIntensity = Mathf.Max(_hazeIntensity, intensity);
            if (_haze == null) _haze = NewQuad("HeatHaze", _hazeMat);
        }

        private void TickHaze(float dt)
        {
            if (_haze == null) return;

            if (_hazeRefresh > 0f)
            {
                _hazeRefresh -= dt;
                _hazeLevel = Mathf.MoveTowards(_hazeLevel, 1f, dt / 0.6f); // build up over ~0.6s of sustained fire
            }
            else
            {
                _hazeLevel = Mathf.MoveTowards(_hazeLevel, 0f, dt / 0.5f);
                _hazeIntensity = Mathf.MoveTowards(_hazeIntensity, 0f, dt);
            }

            if (_hazeLevel <= 0.001f && _hazeRefresh <= 0f)
            {
                if (_haze.Go.activeSelf) _haze.Go.SetActive(false);
                return;
            }

            if (!_haze.Go.activeSelf) _haze.Go.SetActive(true);

            float k = Mathf.InverseLerp(0.6f, 2.5f, _hazeIntensity);
            float size = Mathf.Lerp(1.4f, 2.6f, _hazeLevel) * (0.85f + 0.35f * k);
            _haze.Tr.position = _hazePos + Vector3.up * (0.9f + 0.4f * _hazeLevel);
            _haze.Tr.localScale = new Vector3(size, size, size);
            if (_camera == null) _camera = Camera.main;
            if (_camera != null) _haze.Tr.rotation = Quaternion.LookRotation(_haze.Tr.position - _camera.transform.position);
            _haze.SetLevel(_hazeLevel * Mathf.Lerp(0.7f, 1f, k));
        }

        // --- construction ---------------------------------------------------------------------------

        private Casing NewCasing()
        {
            var go = new GameObject("Casing");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(0.06f, 0.06f, 0.14f);
            go.AddComponent<MeshFilter>().sharedMesh = _cubeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _casingMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new Casing { Go = go, Tr = go.transform };
        }

        private FxQuad NewQuad(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new FxQuad(go, mr);
        }

        private DebuffIndicator NewShredIndicator()
        {
            var root = new GameObject("ArmorShred");
            root.transform.SetParent(transform, false);
            var lr = root.AddComponent<LineRenderer>();
            lr.useWorldSpace = false; // local fracture geometry; the root follows the target
            lr.loop = false;
            lr.material = _lineMat;
            lr.widthMultiplier = 0.05f;
            lr.numCapVertices = 1;
            lr.numCornerVertices = 1;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            root.SetActive(false);
            return new DebuffIndicator { Root = root, Line = lr };
        }

        private LineRenderer NewLine(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.material = _lineMat;
            lr.widthMultiplier = 0.06f;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        // --- geometry -------------------------------------------------------------------------------

        private static void BuildStar(LineRenderer lr, Vector3 center, float radius)
        {
            const int spokes = 4;
            lr.loop = false;
            lr.positionCount = spokes * 2 + 1;
            int idx = 0;
            lr.SetPosition(idx++, center);
            for (int s = 0; s < spokes; s++)
            {
                float a = (s / (float)spokes) * Mathf.PI * 2f + 0.4f;
                var tip = center + new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)) * radius;
                lr.SetPosition(idx++, tip);
                lr.SetPosition(idx++, center);
            }
        }

        // Local-space fracture cracks whose COUNT scales with the current Armor Shredder stack count, so 1 stack
        // is a couple of hairline cracks and max stacks reads as a heavily fractured surface. Random but seeded
        // by stacks so the pattern is stable while at a given stack count.
        private void BuildFracture(LineRenderer lr, int stacks, int maxStacks)
        {
            int cracks = Mathf.Clamp(Mathf.CeilToInt(2f * stacks / Mathf.Max(1, maxStacks) + stacks), 2, 12);
            var rng = new System.Random(stacks * 911 + 17);
            lr.loop = false;
            lr.positionCount = cracks * 3;
            float r = _indicatorRadius;
            for (int i = 0; i < cracks; i++)
            {
                float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float inner = 0.15f + (float)rng.NextDouble() * 0.35f;
                float outer = 0.7f + (float)rng.NextDouble() * 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var perp = new Vector3(-dir.z, 0f, dir.x) * ((float)rng.NextDouble() - 0.5f) * 0.4f;
                lr.SetPosition(i * 3 + 0, dir * (r * inner));
                lr.SetPosition(i * 3 + 1, dir * (r * (inner + outer) * 0.5f) + perp * r);
                lr.SetPosition(i * 3 + 2, dir * (r * outer));
            }
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            temp.SetActive(false);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }

        // --- pooled data ----------------------------------------------------------------------------

        private sealed class Tracer3 { public LineRenderer Line; public float Age, BaseWidth; public Color Color; }
        private sealed class Spark { public LineRenderer Line; public Vector3 Center; public Color Color; public float Age, Life, Scale; }
        private sealed class Casing { public GameObject Go; public Transform Tr; public Vector3 Pos, Vel, Spin; public float Age; }

        private sealed class DebuffIndicator
        {
            public GameObject Root;
            public LineRenderer Line;
            public Transform Target;
            public float Remaining, Age;
            public int Stacks;
        }

        private sealed class FxQuad
        {
            public readonly GameObject Go;
            public readonly Transform Tr;
            private readonly MeshRenderer _renderer;
            private readonly MaterialPropertyBlock _mpb;

            public FxQuad(GameObject go, MeshRenderer renderer)
            {
                Go = go; Tr = go.transform; _renderer = renderer; _mpb = new MaterialPropertyBlock();
            }

            public void SetLevel(float level)
            {
                _mpb.SetFloat(_idHazeLevel, level);
                _renderer.SetPropertyBlock(_mpb);
            }
        }
    }
}
