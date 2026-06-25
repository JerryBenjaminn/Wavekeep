using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 47: renders the high-impact VFX for apex + combo-apex talents — deliberately larger, brighter and
    /// weightier than the Task 45/46 Basic/Ultimate effects. Implements <see cref="IAbilityFeedback"/> and joins
    /// the hero's <see cref="CompositeAbilityFeedback"/>; it reacts only to <see cref="OnApexImpact"/> (fired once
    /// per apex cast from the role==Apex execution points) and <see cref="OnComboFrozenLightning"/> (fired at the
    /// exact moment Lethal Surge consumes a primed target), so nothing rides a separate timer.
    ///
    /// Every trigger also fires the shared <see cref="ApexImpactController"/> weight treatment (screen shake +
    /// flash). Frost apexes reuse the Task 44/45 <c>FrostFx</c> crystal shader (scaled up); lightning apexes use
    /// brighter/denser pooled LineRenderer bolts than Task 46. Frozen Lightning intentionally combines both
    /// palettes: a frost-blue burst immediately followed by a gold lightning strike on the same target.
    /// All effects are pooled and animated on unscaled-independent <see cref="Time.deltaTime"/>, self-contained
    /// (no scene wiring), and tear down cleanly so nothing lingers between casts.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Apex VFX Presenter")]
    public sealed class ApexVfxPresenter : MonoBehaviour, IAbilityFeedback
    {
        private static readonly Color FrostBlue = new Color(0.6f, 0.86f, 1f);
        private static readonly Color Gold = new Color(1f, 0.83f, 0.2f);
        private static readonly Color FlashBlue = new Color(0.55f, 0.8f, 1f);
        private static readonly Color FlashGold = new Color(1f, 0.78f, 0.3f);

        private static int _idAlpha, _idEmission;

        private Material _frostMat;
        private Material _lineMat;
        private Mesh _sphereMesh;
        private Mesh _diskMesh;
        private bool _ready;

        private ApexImpactController _impact;

        private readonly List<FrostAnim> _frost = new List<FrostAnim>();
        private readonly List<RingAnim> _rings = new List<RingAnim>();
        private readonly List<BoltAnim> _bolts = new List<BoltAnim>();
        private readonly List<SparkAnim> _sparks = new List<SparkAnim>();
        private readonly List<Scheduled> _pending = new List<Scheduled>();

        private readonly Stack<RingAnim> _ringFree = new Stack<RingAnim>();
        private readonly Stack<BoltAnim> _boltFree = new Stack<BoltAnim>();
        private readonly Stack<SparkAnim> _sparkFree = new Stack<SparkAnim>();

        private void Awake()
        {
            var frostShader = Resources.Load<Shader>("FrostFx");
            if (frostShader == null)
            {
                Debug.LogWarning("[ApexVfxPresenter] 'FrostFx' shader not found in Resources; apex frost VFX disabled.");
            }
            else
            {
                _idAlpha = Shader.PropertyToID("_Alpha");
                _idEmission = Shader.PropertyToID("_Emission");
                _frostMat = new Material(frostShader);
                _frostMat.SetColor("_Color", FrostBlue);
                _frostMat.SetFloat("_FillBase", 0.2f);
                _frostMat.SetFloat("_CrackStrength", 1.4f);
                _frostMat.SetFloat("_VoronoiScale", 5f);
                _frostMat.SetFloat("_CrackWidth", 0.24f);
                _frostMat.SetFloat("_FresnelBoost", 1.1f);
                _frostMat.SetFloat("_MistAmount", 0f);
            }

            var lineShader = Shader.Find("Sprites/Default")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color");
            _lineMat = new Material(lineShader);

            _sphereMesh = GetPrimitiveMesh(PrimitiveType.Sphere);
            _diskMesh = GetPrimitiveMesh(PrimitiveType.Cylinder);
            _impact = ApexImpactController.GetOrCreate();
            _ready = true;
        }

        // --- IAbilityFeedback: only the apex calls are handled here. ---
        public void OnSingleTargetHit(Vector3 from, Vector3 to) { }
        public void OnAreaOfEffect(Vector3 center, float radius) { }
        public void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnGroundPatch(Vector3 center, float radius, float duration) { }
        public IZoneVisual BeginZone(float bandMinZ, float bandMaxZ) => null;
        public void OnLightningStrike(Vector3 from, Vector3 to, LightningStrikeFlags flags) { }
        public void OnChainJump(Vector3 from, Vector3 to) { }
        public void OnArmorBreak(Transform target, float duration) { }
        public void OnVulnerability(Transform target, float duration) { }

        public void OnApexImpact(Vector3 center, float radius, ApexVfxStyle style)
        {
            if (!_ready) return;
            switch (style)
            {
                case ApexVfxStyle.FrostNova:
                    SpawnFrostBurst(center, Mathf.Max(2.5f, radius), 0.95f, 2.4f, 0.5f);
                    SpawnRing(center, Mathf.Max(2.5f, radius) * 0.95f, FrostBlue, 0.4f);
                    _impact?.Impact(0.45f, 0.24f, FlashBlue, 0.16f, 0.18f);
                    break;

                case ApexVfxStyle.FrostShockwave:
                    // Ring expands to the ACTUAL AoE radius + a disk fills it, so the area reads at a glance.
                    SpawnRing(center, radius, FrostBlue, 0.45f);
                    SpawnFrostDisk(center, radius, 0.7f, 0.4f, 0.55f);
                    _impact?.Impact(0.5f, 0.26f, FlashBlue, 0.18f, 0.2f);
                    break;

                case ApexVfxStyle.LightningStorm:
                    _impact?.Impact(0.5f, 0.26f, FlashGold, 0.18f, 0.2f);
                    SpawnStorm(center, Mathf.Max(2.5f, radius));
                    break;

                case ApexVfxStyle.LightningExecute:
                    SpawnBolt(SkyAbove(center, 0f), center, Gold, 0.32f);
                    SpawnSpark(center, Gold, 1.9f);
                    _impact?.Impact(0.6f, 0.28f, FlashGold, 0.24f, 0.22f);
                    break;
            }
        }

        public void OnComboFrozenLightning(Vector3 center)
        {
            if (!_ready) return;
            // Beat 1 — frost-blue freeze burst now.
            SpawnFrostBurst(center, 3f, 0.95f, 2.4f, 0.5f);
            _impact?.Impact(0.55f, 0.26f, FlashBlue, 0.2f, 0.18f);
            // Beat 2 — gold lightning strike on the SAME target a moment later, with its own gold flash.
            _pending.Add(new Scheduled
            {
                From = SkyAbove(center, 0f),
                To = center,
                Color = Gold,
                Width = 0.34f,
                SparkScale = 2f,
                CamShake = 0.75f,
                CamFlashPeak = 0.28f,
                Due = Time.time + 0.09f
            });
        }

        private void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            TickPending();
            TickFrost(dt);
            TickRings(dt);
            TickBolts(dt);
            TickSparks(dt);
        }

        // --- storm / scheduled bolts ------------------------------------------------------------

        private void SpawnStorm(Vector3 center, float radius)
        {
            const int bolts = 6;
            for (int i = 0; i < bolts; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius;
                var ground = center + new Vector3(r.x, 0f, r.y);
                _pending.Add(new Scheduled
                {
                    From = SkyAbove(ground, Random.Range(-1.5f, 1.5f)),
                    To = ground,
                    Color = Gold,
                    Width = Random.Range(0.14f, 0.2f),
                    SparkScale = 1.1f,
                    CamShake = 0f, // the storm's single shake fired once in OnApexImpact
                    CamFlashPeak = 0f,
                    Due = Time.time + i * 0.05f
                });
            }
        }

        private void TickPending()
        {
            float now = Time.time;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Due > now) continue;
                var s = _pending[i];
                _pending.RemoveAt(i);
                SpawnBolt(s.From, s.To, s.Color, s.Width);
                if (s.SparkScale > 0f) SpawnSpark(s.To, s.Color, s.SparkScale);
                if (s.CamShake > 0f || s.CamFlashPeak > 0f)
                    _impact?.Impact(s.CamShake, 0.24f, FlashGold, s.CamFlashPeak, 0.2f);
            }
        }

        private static Vector3 SkyAbove(Vector3 p, float jitterX) => p + new Vector3(jitterX, 12f, 0f);

        // --- frost burst / disk -----------------------------------------------------------------

        private void SpawnFrostBurst(Vector3 center, float radius, float peakAlpha, float peakEmission, float life)
        {
            if (_frostMat == null) return;
            var a = AcquireFrost(_sphereMesh);
            a.Fx.Transform.position = center;
            a.Age = 0f; a.Life = life;
            a.StartScale = Vector3.one * 0.2f;
            a.EndScale = Vector3.one * (radius * 2f);
            a.PeakAlpha = peakAlpha; a.PeakEmission = peakEmission;
            a.HoldThenFade = false;
            a.Fx.Transform.localScale = a.StartScale;
            a.Fx.SetAlphaEmission(peakAlpha, peakEmission);
            a.Fx.Activate(true);
        }

        private void SpawnFrostDisk(Vector3 center, float radius, float peakAlpha, float peakEmission, float life)
        {
            if (_frostMat == null) return;
            var a = AcquireFrost(_diskMesh);
            a.Fx.Transform.position = new Vector3(center.x, 0.06f, center.z);
            a.Age = 0f; a.Life = life;
            var s = new Vector3(radius * 2f, 0.02f, radius * 2f);
            a.StartScale = s; a.EndScale = s;
            a.PeakAlpha = peakAlpha; a.PeakEmission = peakEmission;
            a.HoldThenFade = true;
            a.Fx.Transform.localScale = s;
            a.Fx.SetAlphaEmission(peakAlpha, peakEmission);
            a.Fx.Activate(true);
        }

        private void TickFrost(float dt)
        {
            for (int i = _frost.Count - 1; i >= 0; i--)
            {
                var a = _frost[i];
                a.Age += dt;
                float t = Mathf.Clamp01(a.Age / a.Life);
                if (!a.HoldThenFade)
                {
                    float ease = 1f - (1f - t) * (1f - t);
                    a.Fx.Transform.localScale = Vector3.Lerp(a.StartScale, a.EndScale, ease);
                    a.Fx.SetAlphaEmission(a.PeakAlpha * (1f - t), a.PeakEmission * (1f - t));
                }
                else
                {
                    float alpha = t < 0.6f ? a.PeakAlpha : Mathf.Lerp(a.PeakAlpha, 0f, (t - 0.6f) / 0.4f);
                    a.Fx.SetAlphaEmission(alpha, a.PeakEmission * (1f - t));
                }

                if (a.Age >= a.Life)
                {
                    a.Fx.Activate(false);
                    _frost.RemoveAt(i);
                    _frostFreeList.Add(a); // mesh-keyed free pool (see AcquireFrost)
                }
            }
        }

        // --- rings ------------------------------------------------------------------------------

        private void SpawnRing(Vector3 center, float targetRadius, Color color, float life)
        {
            var r = _ringFree.Count > 0 ? _ringFree.Pop() : new RingAnim { Line = NewLine("ApexRing", loop: true, width: 0.16f) };
            r.Center = center; r.TargetRadius = targetRadius; r.Color = color;
            r.Age = 0f; r.Life = life;
            r.Line.enabled = true;
            _rings.Add(r);
        }

        private void TickRings(float dt)
        {
            for (int i = _rings.Count - 1; i >= 0; i--)
            {
                var r = _rings[i];
                r.Age += dt;
                float t = Mathf.Clamp01(r.Age / r.Life);
                float ease = 1f - (1f - t) * (1f - t);
                BuildRing(r.Line, r.Center, r.TargetRadius * ease);
                var c = r.Color; c.a = 1f - t;
                r.Line.startColor = c; r.Line.endColor = c;

                if (r.Age >= r.Life)
                {
                    r.Line.enabled = false;
                    _rings.RemoveAt(i);
                    _ringFree.Push(r);
                }
            }
        }

        // --- bolts / sparks (gold lightning) ----------------------------------------------------

        private void SpawnBolt(Vector3 from, Vector3 to, Color color, float width)
        {
            var b = _boltFree.Count > 0 ? _boltFree.Pop() : new BoltAnim { Line = NewLine("ApexBolt", loop: false, width: width) };
            BuildJagged(b.Line, from, to);
            b.Color = color; b.BaseWidth = width; b.Age = 0f; b.Life = 0.12f;
            b.Line.widthMultiplier = width;
            b.Line.startColor = color; b.Line.endColor = color;
            b.Line.enabled = true;
            _bolts.Add(b);
        }

        private void TickBolts(float dt)
        {
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var b = _bolts[i];
                b.Age += dt;
                float k = 1f - Mathf.Clamp01(b.Age / b.Life);
                b.Line.widthMultiplier = b.BaseWidth * k;
                var c = b.Color; c.a = k;
                b.Line.startColor = c; b.Line.endColor = c;

                if (b.Age >= b.Life)
                {
                    b.Line.enabled = false;
                    _bolts.RemoveAt(i);
                    _boltFree.Push(b);
                }
            }
        }

        private void SpawnSpark(Vector3 center, Color color, float scale)
        {
            var s = _sparkFree.Count > 0 ? _sparkFree.Pop() : new SparkAnim { Line = NewLine("ApexSpark", loop: false, width: 0.16f) };
            s.Center = center; s.Color = color; s.Scale = scale; s.Age = 0f; s.Life = 0.16f;
            BuildStar(s.Line, center, scale * 0.7f);
            s.Line.startColor = color; s.Line.endColor = color;
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
                BuildStar(s.Line, s.Center, s.Scale * (0.7f + t * 0.7f));
                var c = s.Color; c.a = 1f - t;
                s.Line.startColor = c; s.Line.endColor = c;

                if (s.Age >= s.Life)
                {
                    s.Line.enabled = false;
                    _sparks.RemoveAt(i);
                    _sparkFree.Push(s);
                }
            }
        }

        // --- pools / construction ---------------------------------------------------------------

        private FrostAnim AcquireFrost(Mesh mesh)
        {
            // Burst (sphere) and disk (cylinder) use different meshes, so a pooled instance is only reusable
            // for the same mesh; tag each with its mesh and only reuse a match.
            for (int i = 0; i < _frostFreeList.Count; i++)
            {
                if (_frostFreeList[i].Mesh == mesh)
                {
                    var reused = _frostFreeList[i];
                    _frostFreeList.RemoveAt(i);
                    _frost.Add(reused);
                    return reused;
                }
            }

            var a = new FrostAnim { Mesh = mesh, Fx = NewFrostFx(mesh) };
            _frost.Add(a);
            return a;
        }

        // FrostAnim uses a list-based free pool (keyed by mesh) instead of the simple stacks above.
        private readonly List<FrostAnim> _frostFreeList = new List<FrostAnim>();

        private FxInstance NewFrostFx(Mesh mesh)
        {
            var go = new GameObject("ApexFrost");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _frostMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new FxInstance(go, mr);
        }

        private LineRenderer NewLine(string name, bool loop, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.material = _lineMat;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        private static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            temp.SetActive(false);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return mesh;
        }

        private static void BuildJagged(LineRenderer lr, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.001f) { lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to); return; }
            Vector3 fwd = delta / len;
            Vector3 side = Vector3.Cross(fwd, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            Vector3 vert = Vector3.Cross(fwd, side).normalized;
            int segments = Mathf.Clamp(Mathf.RoundToInt(len / 1.5f), 6, 18);
            float amp = Mathf.Min(len * 0.06f, 0.7f);
            lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i != 0 && i != segments)
                    p += side * Random.Range(-amp, amp) + vert * Random.Range(-amp, amp);
                lr.SetPosition(i, p);
            }
        }

        private static void BuildStar(LineRenderer lr, Vector3 center, float radius)
        {
            const int spokes = 5;
            lr.loop = false;
            lr.positionCount = spokes * 2 + 1;
            int idx = 0;
            lr.SetPosition(idx++, center);
            for (int s = 0; s < spokes; s++)
            {
                float a = (s / (float)spokes) * Mathf.PI * 2f + 0.3f;
                var tip = center + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * radius;
                lr.SetPosition(idx++, tip);
                lr.SetPosition(idx++, center);
            }
        }

        private static void BuildRing(LineRenderer lr, Vector3 center, float radius)
        {
            const int seg = 36;
            lr.loop = true;
            lr.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.06f, Mathf.Sin(a) * radius));
            }
        }

        // --- data holders -----------------------------------------------------------------------

        private sealed class FxInstance
        {
            public readonly Transform Transform;
            private readonly GameObject _go;
            private readonly MeshRenderer _renderer;
            private readonly MaterialPropertyBlock _mpb;

            public FxInstance(GameObject go, MeshRenderer renderer)
            {
                _go = go; Transform = go.transform; _renderer = renderer; _mpb = new MaterialPropertyBlock();
            }

            public void SetAlphaEmission(float alpha, float emission)
            {
                _mpb.SetFloat(_idAlpha, alpha);
                _mpb.SetFloat(_idEmission, emission);
                _renderer.SetPropertyBlock(_mpb);
            }

            public void Activate(bool on) { if (_go.activeSelf != on) _go.SetActive(on); }
        }

        private sealed class FrostAnim
        {
            public Mesh Mesh;
            public FxInstance Fx;
            public float Age, Life, PeakAlpha, PeakEmission;
            public Vector3 StartScale, EndScale;
            public bool HoldThenFade;
        }

        private sealed class RingAnim { public LineRenderer Line; public Vector3 Center; public float Age, Life, TargetRadius; public Color Color; }
        private sealed class BoltAnim { public LineRenderer Line; public float Age, Life, BaseWidth; public Color Color; }
        private sealed class SparkAnim { public LineRenderer Line; public Vector3 Center; public float Age, Life, Scale; public Color Color; }

        private struct Scheduled
        {
            public Vector3 From, To;
            public Color Color;
            public float Width, SparkScale, CamShake, CamFlashPeak, Due;
        }
    }
}
