#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Runtime;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 81 — post-Task-80 balance tuning (see docs/balance/balance_validation_003.md §6). Applies:
    /// <list type="bullet">
    /// <item><b>R1</b> — GameTier <c>_contactDamageScaling</c> = 0.12 (contact damage rides a damped curve;
    /// the code half of R1 lives in <see cref="DifficultyTierSO"/>/<c>WaveSpawner</c>/<c>EnemyRuntime</c>).</item>
    /// <item><b>R2</b> — the open scene's <see cref="WallRuntime"/> max HP 300 → 1200 (scene serialized value
    /// wins over the code default, so this MUST be run + the scene saved).</item>
    /// <item><b>R3</b> — EvilGod base contact damage 15 → 30 (restores boss wall-pressure identity now that
    /// damping caps late compounding; reverses the Task 64 stopgap against the new 1200 wall).</item>
    /// <item><b>R4</b> — Wave_10..60 per-wave <c>StatMultiplier</c> re-ramped 1.5 → 7.0 (+0.11/wave), removing
    /// the ×1.9 wave-10 double-jump. Waves 1–9 and the wave-60 endpoint (×49 total) are unchanged.</item>
    /// <item><b>R5</b> — Task 80 shop items re-scaled to the 1200 wall (Repair 300 / Reinforced 800 /
    /// Aegis 500 &amp; 40s / Barricade 40s / Tar Field 40s), descriptions updated to match.</item>
    /// </list>
    /// Run "Wavekeep/Setup Task 81 (Balance Tuning)" from the gameplay scene (SampleScene), then SAVE the
    /// scene (Ctrl+S). Idempotent. Editor-only data authoring (CLAUDE.md §3.5 — never mutates SOs at runtime).
    /// NOTE: re-running the older Task 63 setup would overwrite R4's wave column — run Task 81 again after it.
    /// </summary>
    public static class Task81BalanceSetup
    {
        private const string WaveFolder = "Assets/Data/Waves";
        private const string GameTierPath = "Assets/Data/DifficultyTiers/GameTier.asset";
        private const string EvilGodPath = "Assets/Data/Enemies/EvilGod.asset";
        private const string ConsumableFolder = "Assets/Data/Consumables";

        // R1: contact multiplier = 1 + (statMultiplier − 1) × this. Playtest knob order if fresh-solo
        // wave 30 proves out of reach: 0.12 → 0.10, then wall 1200 → 1500 (balance_validation_003 §6).
        private const float ContactDamageScaling = 0.12f;
        // R2
        private const float WallMaxHp = 1200f;
        // R3
        private const float EvilGodContactDamage = 30f;
        // R4: waves 1–9 keep Task 63's +0.05/wave ramp; waves 10–60 ramp linearly 1.5 → 7.0.
        private const int LastWave = 60;
        private const float EarlyRampPerWave = 0.05f;
        private const float MidRampStart = 1.5f;
        private const float MidRampPerWave = 0.11f;

        [MenuItem("Wavekeep/Setup Task 81 (Balance Tuning)")]
        public static void Setup()
        {
            TuneWaveColumn();
            TuneGameTier();
            TuneEvilGod();
            TuneShopItems();

            AssetDatabase.SaveAssets();

            bool wallTouched = TuneSceneWall();

            Debug.Log("[Task81] Balance tuning applied: contact-damage scaling 0.12 (GameTier), EvilGod contact 30, " +
                      "Wave_10..60 column re-ramped 1.5→7.0, shop utility items re-scaled to the 1200 wall." +
                      (wallTouched
                          ? " Scene wall set to 1200 — SAVE THE SCENE (Ctrl+S)."
                          : " No WallRuntime found in the open scene — open SampleScene and re-run to apply the wall HP."));
        }

        // --- R4: per-wave StatMultiplier column ---------------------------------------------------------

        private static void TuneWaveColumn()
        {
            for (int w = 1; w <= LastWave; w++)
            {
                var wave = AssetDatabase.LoadAssetAtPath<WaveConfigSO>($"{WaveFolder}/Wave_{w:00}.asset");
                if (wave == null)
                {
                    Debug.LogWarning($"[Task81] Wave_{w:00}.asset not found — skipping.");
                    continue;
                }

                var so = new SerializedObject(wave);
                so.FindProperty("_statMultiplier").floatValue = PerWaveStatMultiplier(w);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static float PerWaveStatMultiplier(int wave)
        {
            float value = wave <= 9
                ? 1.0f + EarlyRampPerWave * (wave - 1)
                : MidRampStart + MidRampPerWave * (wave - 10);
            return Mathf.Round(value * 100f) / 100f;
        }

        // --- R1: GameTier contact damping ---------------------------------------------------------------

        private static void TuneGameTier()
        {
            var tier = AssetDatabase.LoadAssetAtPath<DifficultyTierSO>(GameTierPath);
            if (tier == null)
            {
                Debug.LogError($"[Task81] GameTier not found at {GameTierPath}.");
                return;
            }

            var so = new SerializedObject(tier);
            so.FindProperty("_contactDamageScaling").floatValue = ContactDamageScaling;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- R3: EvilGod boss contact -------------------------------------------------------------------

        private static void TuneEvilGod()
        {
            var evilGod = AssetDatabase.LoadAssetAtPath<EnemyDefinitionSO>(EvilGodPath);
            if (evilGod == null)
            {
                Debug.LogError($"[Task81] EvilGod not found at {EvilGodPath}.");
                return;
            }

            var so = new SerializedObject(evilGod);
            so.FindProperty("_contactDamage").floatValue = EvilGodContactDamage;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- R5: shop utility items ---------------------------------------------------------------------

        private static void TuneShopItems()
        {
            SetConsumable("WallRepairKit", value: 300f, duration: null,
                "Instantly restore 300 wall HP.");
            SetConsumable("ReinforcedRepair", value: 800f, duration: null,
                "Instantly restore a large amount of wall HP (800).");
            SetConsumable("AegisShield", value: 500f, duration: 40f,
                "The wall gains a 500 HP shield that absorbs damage during the next wave.");
            SetConsumable("ReinforcedBarricade", value: null, duration: 40f,
                "The wall takes 40% less damage for the next wave.");
            SetConsumable("TarField", value: null, duration: 40f,
                "Slows enemies crossing the lane by 40% for the next wave.");
            // Glacial Choke / Flash Freeze intentionally unchanged (validation_003 §6 R5).
        }

        private static void SetConsumable(string assetName, float? value, float? duration, string description)
        {
            var item = AssetDatabase.LoadAssetAtPath<ConsumableDefinitionSO>($"{ConsumableFolder}/{assetName}.asset");
            if (item == null)
            {
                Debug.LogWarning($"[Task81] Consumable '{assetName}' not found — run Setup Task 80 first.");
                return;
            }

            var so = new SerializedObject(item);
            if (value.HasValue) so.FindProperty("_effectValue").floatValue = value.Value;
            if (duration.HasValue) so.FindProperty("_duration").floatValue = duration.Value;
            so.FindProperty("_description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- R2: scene wall HP --------------------------------------------------------------------------

        private static bool TuneSceneWall()
        {
            var wall = Object.FindFirstObjectByType<WallRuntime>(FindObjectsInactive.Include);
            if (wall == null) return false;

            var so = new SerializedObject(wall);
            so.FindProperty("_maxHP").floatValue = WallMaxHp;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(wall.gameObject.scene);
            return true;
        }
    }
}
#endif
