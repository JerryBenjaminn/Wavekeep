using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Wavekeep.Abilities;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Task 46: renders Bolt Striker's electrical VFX — fast gold/yellow lightning bolts and brief impact
    /// flashes, plus the Piercing Bolt and Overload debuff indicators. Implements <see cref="IAbilityFeedback"/>
    /// so it is driven from the SAME single-target execution points as the gameplay (via
    /// <see cref="CompositeAbilityFeedback"/>); the generic/frost methods no-op here. Effect COUNTS and timing
    /// come straight from the runtime (one strike per actual Multi-Strike hit, one bolt per actual Chain
    /// Lightning jump, debuff indicators sized to the real debuff duration) — never hardcoded.
    ///
    /// Deliberately built from pooled <see cref="LineRenderer"/>s on one shared unlit material (no Shader
    /// Graph; matches the hero's "quick bright flash" identity rather than Frost Warden's persistent surface
    /// shaders). Two visually DISTINCT debuff indicators (a reviewer-blocking concern): Piercing Bolt = gold
    /// jagged fracture spokes (armor cracking); Overload = a smooth pulsing gold ring (generic vulnerability).
    /// Both refresh in place per target (no stacking) and tear down on expiry or when their target is
    /// despawned/pooled, so nothing lingers on a reused enemy.
    /// </summary>
    [AddComponentMenu("Wavekeep/Runtime/Lightning VFX Presenter")]
    public sealed class LightningVfxPresenter : MonoBehaviour, IAbilityFeedback
    {
        [Header("Presentation (visual only)")]
        [SerializeField] private float _boltFlash = 0.09f;   // snappy — reads as instant, not a travel
        [SerializeField] private float _sparkFlash = 0.13f;
        [SerializeField] private float _indicatorRadius = 0.85f;

        private static readonly Color Gold = new Color(1f, 0.83f, 0.2f);
        private static readonly Color Amber = new Color(1f, 0.6f, 0.12f);
        private static readonly Color CritWhite = new Color(1f, 0.97f, 0.7f);
        private static readonly Color SpikeWhite = new Color(1f, 1f, 0.92f);
        private static readonly Color ExecuteOrange = new Color(1f, 0.45f, 0.12f);

        private const float StrikeStagger = 0.06f; // gap between Multi-Strike flashes so the hit count is countable

        private Material _lineMat;
        private bool _ready;
        private float _nextStrikeSlot;
        private readonly List<PendingStrike> _pending = new List<PendingStrike>();

        private readonly List<Bolt> _bolts = new List<Bolt>();
        private readonly List<Spark> _sparks = new List<Spark>();
        private readonly List<DebuffIndicator> _armorBreaks = new List<DebuffIndicator>();
        private readonly List<DebuffIndicator> _vulnerabilities = new List<DebuffIndicator>();

        private readonly Stack<Bolt> _boltFree = new Stack<Bolt>();
        private readonly Stack<Spark> _sparkFree = new Stack<Spark>();
        private readonly Stack<DebuffIndicator> _armorFree = new Stack<DebuffIndicator>();
        private readonly Stack<DebuffIndicator> _vulnFree = new Stack<DebuffIndicator>();

        private void Awake()
        {
            // Sprites/Default is unlit, transparent and honours per-renderer vertex colours under URP — the
            // same robust choice the Task 08 indicator presenter uses for runtime lines.
            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            _lineMat = new Material(shader);
            _ready = true;
        }

        // --- IAbilityFeedback: generic + frost calls belong to other presenters (no-op here). ---
        public void OnSingleTargetHit(Vector3 from, Vector3 to) { }
        public void OnAreaOfEffect(Vector3 center, float radius) { }
        public void OnRangedImpactBurst(Vector3 from, Vector3 to, float burstRadius) { }
        public void OnGroundPatch(Vector3 center, float radius, float duration) { }
        public IZoneVisual BeginZone(float bandMinZ, float bandMaxZ) => null;

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

        public void OnLightningStrike(Vector3 from, Vector3 to, LightningStrikeFlags flags)
        {
            if (!_ready) return;

            // Multi-Strike resolves all its hits on ONE frame; if every bolt fired now they'd overlap into a
            // single blur and the player couldn't count them. Queue strikes onto staggered time-slots so a
            // 2/3/4-hit burst pops as that many distinct flashes. A lone basic hit spawns effectively instantly.
            float due = Mathf.Max(Time.time, _nextStrikeSlot);
            _nextStrikeSlot = due + StrikeStagger;
            _pending.Add(new PendingStrike { From = from, To = to, Flags = flags, Due = due });
        }

        public void OnChainJump(Vector3 from, Vector3 to)
        {
            if (!_ready) return;
            // Visibly distinct: thinner + dimmer than the main strike (it's the reduced-damage jump).
            SpawnBolt(from, to, Gold, width: 0.07f, jaggedness: 0.8f, dim: 0.55f);
            SpawnSpark(to, Gold, scale: 0.55f);
        }

        public void OnArmorBreak(Transform target, float duration) =>
            RefreshOrSpawnIndicator(_armorBreaks, _armorFree, target, duration, isRing: false, Gold);

        public void OnVulnerability(Transform target, float duration) =>
            RefreshOrSpawnIndicator(_vulnerabilities, _vulnFree, target, duration, isRing: true, Amber);

        // --- styling ----------------------------------------------------------------------------

        private static void Style(LightningStrikeFlags flags, out Color color, out float width, out float flashScale)
        {
            color = Gold;
            width = 0.12f;
            flashScale = 1f;

            if ((flags & LightningStrikeFlags.Ultimate) != 0) { width = 0.22f; flashScale = 1.7f; } // heavier nuke
            if ((flags & LightningStrikeFlags.Crit) != 0) { color = CritWhite; width *= 1.35f; flashScale *= 1.4f; }
            if ((flags & LightningStrikeFlags.Execute) != 0) { color = ExecuteOrange; width *= 1.35f; flashScale *= 1.5f; }
            if ((flags & LightningStrikeFlags.Spike) != 0) { color = SpikeWhite; width *= 1.7f; flashScale *= 1.9f; } // most intense
        }

        // --- main strikes (staggered) -----------------------------------------------------------

        private void TickPendingStrikes()
        {
            float now = Time.time;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Due > now) continue;
                var s = _pending[i];
                _pending.RemoveAt(i);
                Style(s.Flags, out Color color, out float width, out float flashScale);
                SpawnBolt(s.From, s.To, color, width, jaggedness: 1f, dim: 1f);
                SpawnSpark(s.To, color, flashScale);
            }
        }

        // --- bolts ------------------------------------------------------------------------------

        private void SpawnBolt(Vector3 from, Vector3 to, Color color, float width, float jaggedness, float dim)
        {
            var b = _boltFree.Count > 0 ? _boltFree.Pop() : NewBolt();
            BuildJagged(b.Line, from, to, jaggedness);
            var c = color; c.a = dim;
            b.Line.startColor = c;
            b.Line.endColor = c;
            b.Line.widthMultiplier = width;
            b.BaseWidth = width;
            b.Color = c;
            b.Age = 0f;
            b.Life = _boltFlash;
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
                var c = b.Color; c.a = b.Color.a * k;
                b.Line.startColor = c;
                b.Line.endColor = c;

                if (b.Age >= b.Life)
                {
                    b.Line.enabled = false;
                    _bolts.RemoveAt(i);
                    _boltFree.Push(b);
                }
            }
        }

        // --- impact sparks (radial star) --------------------------------------------------------

        private void SpawnSpark(Vector3 center, Color color, float scale)
        {
            var s = _sparkFree.Count > 0 ? _sparkFree.Pop() : NewSpark();
            s.Center = center;
            s.Color = color;
            s.Scale = scale;
            s.Age = 0f;
            s.Life = _sparkFlash;
            BuildStar(s.Line, center, scale * 0.6f);
            s.Line.startColor = color;
            s.Line.endColor = color;
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
                float k = 1f - t;
                BuildStar(s.Line, s.Center, s.Scale * (0.6f + t * 0.6f)); // expand slightly as it fades
                var c = s.Color; c.a = k;
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

        // --- debuff indicators ------------------------------------------------------------------

        private void RefreshOrSpawnIndicator(List<DebuffIndicator> active, Stack<DebuffIndicator> free,
            Transform target, float duration, bool isRing, Color color)
        {
            if (!_ready || target == null || duration <= 0f) return;

            // Refresh in place if this target already has one (debuffs refresh, they don't stack visually).
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Target == target)
                {
                    active[i].Remaining = Mathf.Max(active[i].Remaining, duration);
                    return;
                }
            }

            var d = free.Count > 0 ? free.Pop() : NewIndicator(isRing);
            d.Target = target;
            d.Remaining = duration;
            d.Age = 0f;
            d.Color = color;
            d.Root.SetActive(true);
            d.Root.transform.position = target.position;
            active.Add(d);
        }

        private void TickIndicators(List<DebuffIndicator> active, Stack<DebuffIndicator> free, bool isRing, float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var d = active[i];
                d.Remaining -= dt;
                d.Age += dt;

                // A pooled (released) enemy is deactivated, so its indicator tears down cleanly.
                bool targetGone = d.Target == null || !d.Target.gameObject.activeInHierarchy;
                if (d.Remaining <= 0f || targetGone)
                {
                    d.Root.SetActive(false);
                    d.Target = null;
                    active.RemoveAt(i);
                    free.Push(d);
                    continue;
                }

                // Follow the (moving) target and pulse.
                d.Root.transform.position = d.Target.position;
                float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(d.Age * 6f));
                var c = d.Color; c.a = pulse;
                d.Line.startColor = c;
                d.Line.endColor = c;

                if (isRing)
                    BuildRing(d.Line, _indicatorRadius * (0.9f + 0.15f * Mathf.Sin(d.Age * 6f)));
                // fracture spokes are static in shape; only the pulse colour animates.
            }
        }

        private void Update()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            TickPendingStrikes();
            TickBolts(dt);
            TickSparks(dt);
            TickIndicators(_armorBreaks, _armorFree, isRing: false, dt);
            TickIndicators(_vulnerabilities, _vulnFree, isRing: true, dt);
        }

        // --- geometry builders ------------------------------------------------------------------

        private static void BuildJagged(LineRenderer lr, Vector3 from, Vector3 to, float jaggedness)
        {
            Vector3 delta = to - from;
            float len = delta.magnitude;
            if (len < 0.001f) { lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to); return; }

            Vector3 fwd = delta / len;
            Vector3 side = Vector3.Cross(fwd, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            Vector3 vert = Vector3.Cross(fwd, side).normalized;

            int segments = Mathf.Clamp(Mathf.RoundToInt(len / 2f), 6, 16);
            float amp = Mathf.Min(len * 0.05f, 0.55f) * jaggedness;

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
            // 4 spokes drawn as one polyline (out-and-back through the centre).
            const int spokes = 4;
            lr.loop = false;
            lr.positionCount = spokes * 2 + 1;
            int idx = 0;
            lr.SetPosition(idx++, center);
            for (int s = 0; s < spokes; s++)
            {
                float a = (s / (float)spokes) * Mathf.PI * 2f + 0.4f;
                var tip = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                lr.SetPosition(idx++, tip);
                lr.SetPosition(idx++, center);
            }
        }

        // Jagged radiating fracture spokes (Piercing Bolt) around a local centre, in the XZ plane.
        private void BuildFracture(LineRenderer lr)
        {
            const int spokes = 5;
            lr.loop = false;
            lr.positionCount = spokes * 3 + 1;
            int idx = 0;
            lr.SetPosition(idx++, Vector3.zero);
            for (int s = 0; s < spokes; s++)
            {
                float a = (s / (float)spokes) * Mathf.PI * 2f + 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var perp = new Vector3(-dir.z, 0f, dir.x);
                var mid = dir * (_indicatorRadius * 0.55f) + perp * (_indicatorRadius * 0.18f);
                var tip = dir * _indicatorRadius;
                lr.SetPosition(idx++, mid);
                lr.SetPosition(idx++, tip);
                lr.SetPosition(idx++, Vector3.zero);
            }
        }

        private static void BuildRing(LineRenderer lr, float radius)
        {
            const int seg = 28;
            lr.loop = true;
            lr.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        // --- construction -----------------------------------------------------------------------

        private Bolt NewBolt() => new Bolt { Line = NewLine("Bolt", worldSpace: true, loop: false) };
        private Spark NewSpark() => new Spark { Line = NewLine("Spark", worldSpace: true, loop: false) };

        private DebuffIndicator NewIndicator(bool isRing)
        {
            var root = new GameObject(isRing ? "Vulnerability" : "ArmorBreak");
            root.transform.SetParent(transform, false);
            var lr = CreateLineOn(root, loop: isRing);
            lr.useWorldSpace = false; // indicator geometry is local; the root follows the target
            lr.widthMultiplier = isRing ? 0.06f : 0.07f;
            var d = new DebuffIndicator { Root = root, Line = lr };
            if (isRing) BuildRing(lr, _indicatorRadius);
            else BuildFracture(lr);
            root.SetActive(false);
            return d;
        }

        private LineRenderer NewLine(string name, bool worldSpace, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = CreateLineOn(go, loop);
            lr.useWorldSpace = worldSpace;
            lr.enabled = false;
            return lr;
        }

        private LineRenderer CreateLineOn(GameObject go, bool loop)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = loop;
            lr.material = _lineMat;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.widthMultiplier = 0.12f;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.View; // face the camera so thin bolts stay visible from the 3/4 angle
            return lr;
        }

        // --- pooled data --------------------------------------------------------------------------

        private struct PendingStrike { public Vector3 From, To; public LightningStrikeFlags Flags; public float Due; }

        private sealed class Bolt { public LineRenderer Line; public float Age, Life, BaseWidth; public Color Color; }
        private sealed class Spark { public LineRenderer Line; public Vector3 Center; public Color Color; public float Age, Life, Scale; }

        private sealed class DebuffIndicator
        {
            public GameObject Root;
            public LineRenderer Line;
            public Transform Target;
            public float Remaining, Age;
            public Color Color;
        }
    }
}
