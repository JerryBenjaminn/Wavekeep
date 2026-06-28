#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Core;
using Wavekeep.Waves;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 63 — balance tuning pass (data authoring). Implements the developer-approved direction from the
    /// Task 61 audit + Task 62 bug fixes:
    /// <list type="bullet">
    /// <item><b>§1.1 Wave curve</b> — rewrites Wave_01..60's per-wave <c>StatMultiplier</c> into a monotonic
    /// non-decreasing checkpoint curve: a gentle +5%/wave ramp, with the tier's every-5th-wave milestone step
    /// (the existing <see cref="DifficultyTierSO.GetMilestoneMultiplier"/> mechanic) providing the deliberate
    /// spike. No new curve-evaluation code — the shape is authored into existing fields.</item>
    /// <item><b>§1.2 Content extension</b> — creates Wave_21..60 (Skeleton trash + EvilGod boss roster) and wires
    /// all 60 into <c>GameTier</c>, so the wave-15/30/50 hero-slot unlocks are finally reachable.</item>
    /// <item><b>§1.3 EvilGod identity</b> — slow heavy tank: move speed cut to ~53% of Skeleton's. (Contact 30 /
    /// currency 50 already satisfy §1.3's 4–6× / 8–12× targets from Task 62; re-asserted here, HP 200 unchanged
    /// and scales via the curve at each boss appearance.)</item>
    /// <item><b>§1.5 Gear rarity spread</b> — widens every (slot × rarity) item's modifier by a per-rarity power
    /// factor so each tier is a clear step up, preserving each slot's existing modifier TYPE.</item>
    /// <item><b>§1.6 Regular loot</b> — replaces Task 62's even-weight placeholder with a common-favoring spread
    /// across Common→Epic. Legendary &amp; Unique are EXCLUDED (boss-exclusive per CLAUDE.md §6 — see the FLAG in
    /// the task summary; the task's suggested "Legendary 1%" would violate that locked decision).</item>
    /// <item>Switches the open scene's <see cref="WaveSpawner"/> to <c>GameTier</c> and re-asserts the bootstrap's
    /// XP quadratic coefficient (§1.4 is otherwise a code change in <c>XPManager</c>/<c>GameSessionBootstrap</c>).</item>
    /// </list>
    /// Run "Wavekeep/Setup Task 63 (Balance Tuning)" from the gameplay scene (SampleScene), then save it (Ctrl+S).
    /// Idempotent. Editor-only. Never mutates SOs at runtime — this is editor-time data authoring (CLAUDE.md §3.5).
    /// </summary>
    public static class Task63BalanceSetup
    {
        private const int FirstWave = 1;
        private const int LastWave = 60;

        private const string WaveFolder = "Assets/Data/Waves";
        private const string GearFolder = "Assets/Data/Gear";
        private const string GameTierPath = "Assets/Data/DifficultyTiers/GameTier.asset";
        private const string SkeletonPath = "Assets/Data/Enemies/Skeleton.asset";
        private const string EvilGodPath = "Assets/Data/Enemies/EvilGod.asset";
        private const string LootRegularPath = "Assets/Data/Loot/LootTable_Regular.asset";
        private const string BossEarlyPath = "Assets/Data/Loot/LootTable_BossEarly.asset";
        private const string BossLatePath = "Assets/Data/Loot/LootTable_BossLate.asset";

        // §1.1 curve params (authored into existing fields; engine math unchanged).
        private const float GlobalStatMultiplier = 1.0f;   // GameTier global (clean baseline)
        private const float PerWaveRampPerWave = 0.05f;     // +5%/wave gradual ramp within a block
        private const float MilestoneStep = 0.5f;           // every-5th-wave spike (tier milestone)
        private const int MilestoneInterval = 5;
        private const int BossInterval = 5;

        // §1.5 per-rarity power factor, indexed by (int)Rarity (Common..Unique). Common kept at 1.05 (not 1.00)
        // so Common multiplier-slots aren't dead weight; widens steeply at the top (Legendary/Unique).
        private static readonly float[] RarityPower = { 1.05f, 1.18f, 1.35f, 1.60f, 2.00f, 2.60f };

        [MenuItem("Wavekeep/Setup Task 63 (Balance Tuning)")]
        public static void Setup()
        {
            var skeleton = Load<EnemyDefinitionSO>(SkeletonPath);
            var evilGod = Load<EnemyDefinitionSO>(EvilGodPath);
            var bossEarly = Load<LootTableSO>(BossEarlyPath);
            var bossLate = Load<LootTableSO>(BossLatePath);
            if (skeleton == null || evilGod == null)
            {
                Debug.LogError("[Task63] Skeleton/EvilGod enemy assets not found — aborting.");
                return;
            }

            var waves = BuildWaves(skeleton, bossEarly, bossLate);
            AssetDatabase.SaveAssets();

            ConfigureGameTier(waves, evilGod);
            TuneEvilGod(evilGod);
            TuneGear();
            TuneRegularLoot();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SwitchSceneToGameTier();

            Debug.Log($"[Task63] Balance tuning complete: Wave_01..{LastWave:00} authored (checkpoint curve), " +
                      "GameTier wired (boss EvilGod every 5), EvilGod slowed to a heavy tank, gear rarity spread " +
                      "widened, LootTable_Regular re-weighted (common-favoring, Common→Epic). If the scene's " +
                      "WaveSpawner/bootstrap were updated, SAVE the scene (Ctrl+S). See the task summary for the " +
                      "FLAG on Legendary boss-exclusivity (§1.6 vs CLAUDE.md §6).");
        }

        // --- §1.1 / §1.2 Waves -------------------------------------------------------------------------

        private static List<WaveConfigSO> BuildWaves(EnemyDefinitionSO skeleton, LootTableSO bossEarly, LootTableSO bossLate)
        {
            var list = new List<WaveConfigSO>(LastWave);
            for (int w = FirstWave; w <= LastWave; w++)
            {
                var wave = AbilityAssetUtil.LoadOrCreate<WaveConfigSO>($"{WaveFolder}/Wave_{w:00}.asset");
                var so = new SerializedObject(wave);

                so.FindProperty("_waveNumber").intValue = w;
                so.FindProperty("_statMultiplier").floatValue = PerWaveStatMultiplier(w);

                // Single Skeleton entry, escalating count + tightening spawn interval (Skeleton/EvilGod roster, §1.2).
                var entries = so.FindProperty("_spawnEntries");
                entries.arraySize = 1;
                var e0 = entries.GetArrayElementAtIndex(0);
                e0.FindPropertyRelative("_enemyType").objectReferenceValue = skeleton;
                e0.FindPropertyRelative("_count").intValue = SkeletonCount(w);
                e0.FindPropertyRelative("_spawnInterval").floatValue = SpawnInterval(w);

                // Boss waves (every 5th): the boss itself is spawned by the tier (BossDefinition), but the WAVE
                // carries the loot table the boss drops (Task 13 keying). Early boss waves → BossEarly, later → BossLate.
                bool bossWave = w % BossInterval == 0;
                var bossLoot = so.FindProperty("_bossLootTable");
                bossLoot.objectReferenceValue = !bossWave ? null : (w <= 25 ? (Object)bossEarly : bossLate);

                so.ApplyModifiedPropertiesWithoutUndo();
                list.Add(wave);
            }
            return list;
        }

        // Monotonic non-decreasing gentle ramp; the tier milestone supplies the every-5th-wave spike on top.
        private static float PerWaveStatMultiplier(int wave) => Round2(1.0f + PerWaveRampPerWave * (wave - 1));

        // Escalating trash count: 19 (wave 1) → 78 (wave 60). Boss waves (every 5th) carry a LIGHTER escort
        // (~half) so the boss is the centrepiece and a forced-solo single-target hero (slot 2 unlocks at
        // wave 15) can split focus between the swarm and the boss instead of drowning in trash. Task 64
        // follow-up after playtesting (Bolt Striker couldn't clear the wave-5 boss solo).
        private static int SkeletonCount(int wave)
        {
            int baseCount = 18 + wave;
            return wave % BossInterval == 0 ? Mathf.RoundToInt(baseCount * 0.5f) : baseCount;
        }

        // Spawn pressure tightens with waves: 0.45s (wave 1) → ~0.18s (wave 60).
        private static float SpawnInterval(int wave) => Round2(Mathf.Clamp(0.45f - 0.0045f * (wave - 1), 0.18f, 0.45f));

        // --- §1.2 GameTier wiring ----------------------------------------------------------------------

        private static void ConfigureGameTier(List<WaveConfigSO> waves, EnemyDefinitionSO evilGod)
        {
            var tier = Load<DifficultyTierSO>(GameTierPath);
            if (tier == null)
            {
                Debug.LogError($"[Task63] GameTier not found at {GameTierPath} — cannot wire waves.");
                return;
            }

            var so = new SerializedObject(tier);
            so.FindProperty("_tierName").stringValue = "Normal";
            so.FindProperty("_globalStatMultiplier").floatValue = GlobalStatMultiplier;
            so.FindProperty("_milestoneWaveInterval").intValue = MilestoneInterval;
            so.FindProperty("_milestoneStep").floatValue = MilestoneStep;
            so.FindProperty("_bossWaveInterval").intValue = BossInterval;
            so.FindProperty("_bossDefinition").objectReferenceValue = evilGod; // §1.3: EvilGod is GameTier's boss
            so.FindProperty("_bossCount").intValue = 1;

            var list = so.FindProperty("_waves");
            list.arraySize = waves.Count;
            for (int i = 0; i < waves.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- §1.3 EvilGod identity ---------------------------------------------------------------------

        private static void TuneEvilGod(EnemyDefinitionSO evilGod)
        {
            var so = new SerializedObject(evilGod);
            so.FindProperty("_moveSpeed").floatValue = 1.6f;       // ~53% of Skeleton's 3 → lumbering heavy tank
            // Task 64: lowered 30 (6×) → 15 (3× Skeleton). The wave-5 checkpoint multiplier (1.8×) compounded the
            // old 6× base into 54/hit on the 300-HP wall — lethal for a single under-geared hero. 3× keeps the
            // "heavy hitter" identity while halving first-boss damage; the ratio over Skeleton is constant, so
            // later boss waves (10/15/20) scale with the curve, not faster.
            so.FindProperty("_contactDamage").floatValue = 15f;    // 3× Skeleton (Task 64)
            so.FindProperty("_currencyReward").intValue = 50;      // 10× Skeleton (re-assert Task 62 stopgap)
            // _maxHealth (200) intentionally untouched — it scales via the §1.1 curve at each boss appearance.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- §1.5 Gear rarity spread -------------------------------------------------------------------

        private static void TuneGear()
        {
            var guids = AssetDatabase.FindAssets("t:GearItemSO t:ArtifactItemSO", new[] { GearFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<LootItemSO>(path);
                if (item == null) continue;

                var so = new SerializedObject(item);
                int rarity = so.FindProperty("_rarity").enumValueIndex;
                GearSlot slot = item.Slot; // GearItemSO returns its authored slot; ArtifactItemSO returns Artifact

                var (type, value) = ModifierFor(slot, rarity);

                var mods = so.FindProperty("_statModifiers");
                if (mods.arraySize < 1) mods.arraySize = 1;
                var el = mods.GetArrayElementAtIndex(0);
                el.FindPropertyRelative("_modifierType").enumValueIndex = (int)type;
                el.FindPropertyRelative("_value").floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Per-slot modifier TYPE preserved (matches the existing data); VALUE widened by the per-rarity power factor.
        private static (AbilityModifierType type, float value) ModifierFor(GearSlot slot, int rarity)
        {
            float p = RarityPower[Mathf.Clamp(rarity, 0, RarityPower.Length - 1)];
            switch (slot)
            {
                case GearSlot.Body:     return (AbilityModifierType.DamageMultiplier, Round2(p));
                case GearSlot.Artifact: return (AbilityModifierType.DamageMultiplier, Round2(p + 0.10f)); // premium slot
                case GearSlot.Helmet:   return (AbilityModifierType.DamageFlatBonus, Mathf.Round(8f * p));
                case GearSlot.Feet:     return (AbilityModifierType.DamageFlatBonus, Mathf.Round(10f * p));
                case GearSlot.Hands:    return (AbilityModifierType.CooldownMultiplier, Round2(1f / p));  // lower = faster
                case GearSlot.Legs:     return (AbilityModifierType.RangeMultiplier, Round2(1f + 0.5f * (p - 1f)));
                default:                return (AbilityModifierType.DamageMultiplier, Round2(p));
            }
        }

        // --- §1.6 Regular loot weighting ---------------------------------------------------------------

        // Common-favoring spread, Common→Epic only. Legendary & Unique are boss-exclusive (CLAUDE.md §6) so they
        // are NOT in the regular table — this deliberately drops the task's suggested "Legendary 1%" (flagged).
        // Per-rarity total weight: Common 60, Uncommon 25, Rare 10, Epic 5 (sum 100 → readable percentages).
        private static readonly (Rarity rarity, int perItemWeight)[] RegularSpread =
        {
            (Rarity.Common,   12), // ×5 items = 60
            (Rarity.Uncommon,  5), // ×5 items = 25
            (Rarity.Rare,      2), // ×5 items = 10
            (Rarity.Epic,      1), // ×5 items = 5
        };

        private static readonly string[] ArmorNouns = { "Body", "Feet", "Hands", "Helm", "Legs" };

        private static void TuneRegularLoot()
        {
            var table = Load<LootTableSO>(LootRegularPath);
            if (table == null)
            {
                Debug.LogWarning($"[Task63] {LootRegularPath} not found — skipping regular-loot reweight.");
                return;
            }

            var so = new SerializedObject(table);
            var entries = so.FindProperty("_entries");
            entries.ClearArray();

            int idx = 0;
            foreach (var (rarity, weight) in RegularSpread)
            {
                foreach (var noun in ArmorNouns)
                {
                    var gear = AssetDatabase.LoadAssetAtPath<GearItemSO>($"{GearFolder}/Gear_{rarity}{noun}.asset");
                    if (gear == null)
                    {
                        Debug.LogWarning($"[Task63] Missing gear asset Gear_{rarity}{noun} — skipped from regular loot.");
                        continue;
                    }
                    entries.arraySize = idx + 1;
                    var el = entries.GetArrayElementAtIndex(idx++);
                    el.FindPropertyRelative("_item").objectReferenceValue = gear;
                    el.FindPropertyRelative("_weight").intValue = weight;
                }
            }

            // _dropChance left as-is (Task 62 set the 2.8% roll; §1.6 only governs the weight distribution).
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- Scene wiring ------------------------------------------------------------------------------

        private static void SwitchSceneToGameTier()
        {
            var tier = Load<DifficultyTierSO>(GameTierPath);
            bool dirtied = false;

            var spawner = Object.FindFirstObjectByType<WaveSpawner>();
            if (spawner != null && tier != null)
            {
                var so = new SerializedObject(spawner);
                so.FindProperty("_difficultyTier").objectReferenceValue = tier;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirtied = true;
                Debug.Log("[Task63] Switched the open scene's WaveSpawner to GameTier (the now-populated live tier).");
            }
            else
            {
                Debug.LogWarning("[Task63] No WaveSpawner in the open scene — open SampleScene and re-run to switch " +
                                 "the live tier to GameTier.");
            }

            // §1.4: re-assert the XP quadratic coefficient on the bootstrap (the field defaults to 2, but set it
            // explicitly so an already-serialized scene reflects it without relying on the default).
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap != null)
            {
                var so = new SerializedObject(bootstrap);
                var prop = so.FindProperty("_xpQuadraticPerLevel");
                if (prop != null) { prop.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); dirtied = true; }
            }

            if (dirtied)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // --- helpers -----------------------------------------------------------------------------------

        private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
        private static float Round2(float v) => Mathf.Round(v * 100f) / 100f;
    }
}
#endif
