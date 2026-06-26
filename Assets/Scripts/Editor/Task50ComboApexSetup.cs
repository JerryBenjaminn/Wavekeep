#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 50 — authors the four remaining cross-hero combo apexes and wires them into the gameplay scene,
    /// completing the combo matrix alongside Frozen Lightning (Task 38). Each reuses the existing
    /// <see cref="ComboApexTalentDefinitionSO"/> + <c>Passive</c> trigger; the new <see cref="ComboEffectType"/>
    /// discriminator selects which passive RULE each one layers onto its two referenced apexes:
    /// <list type="bullet">
    /// <item><b>Shatter</b> (Remorseless Winter + Bullet Storm) — ShatterDetonate: RW primes on freeze; any
    ///   Physical hit on a primed target detonates a 3m AoE for ×2.0 of the shot's damage, consuming the prime.</item>
    /// <item><b>Frostburn</b> (Remorseless Winter + Cataclysm) — FrostburnTick: Burn ticks deal ×1.75 while the
    ///   target has any active Slow/Freeze (continuous per-tick check, not a consumed prime).</item>
    /// <item><b>Chain Combustion</b> (Thunderstorm + Wildfire Apocalypse) — ChainCombustion: a chain-jump on a
    ///   Burning target extends its Burn +2s and adds one Stacking-Embers stack.</item>
    /// <item><b>Incendiary Rounds</b> (Executioner's Volley + Cataclysm) — IncendiaryPierce: every pierced enemy
    ///   beyond the first also gets a Burn at the held Smoldering Wound tier potency (base 3/tick, 3s).</item>
    /// </list>
    ///
    /// SCHEMA NOTE (flagged per the task): this required a small, justified extension to
    /// <see cref="ComboApexTalentDefinitionSO"/> — a <see cref="ComboEffectType"/> enum + a few generic numeric
    /// fields — because three of the four are not prime/consume at all and the resolver could not otherwise tell
    /// a "×1.75 Burn" combo from a "×2.5 consume" combo. It is the minimal generic extension (no new SO type).
    ///
    /// Run "Wavekeep/Setup Task 50 (Combo Apexes)" in the gameplay scene AFTER the Task 31/35/48/49 content
    /// setups (the six referenced apex assets must exist). Idempotent. Editor-only.
    /// </summary>
    public static class Task50ComboApexSetup
    {
        private const string ComboFolder = "Assets/Data/ComboApexes";
        private const string LineFolder = "Assets/Data/UpgradeLines";

        [MenuItem("Wavekeep/Setup Task 50 (Combo Apexes)")]
        public static void SetupScene()
        {
            var remorselessWinter = LoadApex("ApexTalent_RemorselessWinter");
            var bulletStorm = LoadApex("ApexTalent_BulletStorm");
            var cataclysm = LoadApex("ApexTalent_Cataclysm");
            var thunderstorm = LoadApex("ApexTalent_Thunderstorm");
            var wildfireApocalypse = LoadApex("ApexTalent_WildfireApocalypse");
            var executionersVolley = LoadApex("ApexTalent_ExecutionersVolley");

            if (remorselessWinter == null || bulletStorm == null || cataclysm == null ||
                thunderstorm == null || wildfireApocalypse == null || executionersVolley == null)
            {
                Debug.LogError("[Task50] Missing one or more apex assets. Run the Task 31/35/48/49 content setups first.");
                return;
            }

            EnsureFolder("Assets/Data", "ComboApexes");

            // 1. Shatter — Remorseless Winter (primer) + Bullet Storm. Physical hit on a primed target detonates.
            var shatter = Combo("ComboApex_Shatter", "Shatter", ComboEffectType.ShatterDetonate,
                remorselessWinter, bulletStorm,
                "Frost Warden + Marksman: Remorseless Winter primes frozen targets; any Physical Marksman shot " +
                "detonates a primed target for a Physical AoE burst (×2.0 of the shot), then consumes the prime.",
                primeWindow: 2f, multiplier: 2.0f, effectRadius: 3f, burnExtend: 0f, ignitePerTick: 0f, igniteDuration: 0f);

            // 2. Frostburn — Remorseless Winter + Cataclysm. Burn ticks ×1.75 while target has Slow/Freeze.
            var frostburn = Combo("ComboApex_Frostburn", "Frostburn", ComboEffectType.FrostburnTick,
                remorselessWinter, cataclysm,
                "Frost Warden + Pyromancer: Burn ticks deal ×1.75 damage while the target is under any Frost " +
                "Warden Slow/Freeze (a continuous per-tick check).",
                primeWindow: 0f, multiplier: 1.75f, effectRadius: 0f, burnExtend: 0f, ignitePerTick: 0f, igniteDuration: 0f);

            // 3. Chain Combustion — Thunderstorm + Wildfire Apocalypse. Chain-jump extends Burn + adds a stack.
            var chainCombustion = Combo("ComboApex_ChainCombustion", "Chain Combustion", ComboEffectType.ChainCombustion,
                thunderstorm, wildfireApocalypse,
                "Bolt Striker + Pyromancer: a Chain Lightning jump onto a Burning target extends its Burn by 2s " +
                "and adds one Stacking Embers stack — no Fireball needed.",
                primeWindow: 0f, multiplier: 1f, effectRadius: 0f, burnExtend: 2f, ignitePerTick: 0f, igniteDuration: 0f);

            // 4. Incendiary Rounds — Executioner's Volley + Cataclysm. Pierced-beyond-first targets get a Burn.
            var incendiaryRounds = Combo("ComboApex_IncendiaryRounds", "Incendiary Rounds", ComboEffectType.IncendiaryPierce,
                executionersVolley, cataclysm,
                "Marksman + Pyromancer: every enemy a Marksman pierce shot hits beyond the first also receives a " +
                "Burn at the current Smoldering Wound tier potency.",
                primeWindow: 0f, multiplier: 1f, effectRadius: 0f, burnExtend: 0f, ignitePerTick: 3f, igniteDuration: 3f);

            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>();
            if (bootstrap != null)
            {
                WireBootstrap(bootstrap, shatter);
                WireBootstrap(bootstrap, frostburn);
                WireBootstrap(bootstrap, chainCombustion);
                WireBootstrap(bootstrap, incendiaryRounds);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            else
            {
                Debug.LogWarning("[Task50] No GameSessionBootstrap in the open scene — the four combo assets were " +
                                 "authored but not wired into a run. Open the gameplay scene and re-run.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Keep the Codex's master talent list current so the four new combos are discoverable.
            Task43CodexSetup.EnsureCatalog();

            Debug.Log("[Task50] Authored 4 combo apexes (Shatter, Frostburn, Chain Combustion, Incendiary Rounds) " +
                      "and wired them into the scene bootstrap. With Frozen Lightning, the full combo matrix is set. " +
                      "Save the scene (Ctrl+S).");
        }

        private static ComboApexTalentDefinitionSO Combo(string file, string name, ComboEffectType effect,
            ApexTalentDefinitionSO primer, ApexTalentDefinitionSO consumer, string description,
            float primeWindow, float multiplier, float effectRadius, float burnExtend,
            float ignitePerTick, float igniteDuration)
        {
            var combo = AbilityAssetUtil.LoadOrCreate<ComboApexTalentDefinitionSO>($"{ComboFolder}/{file}.asset");
            var so = new SerializedObject(combo);
            so.FindProperty("_comboName").stringValue = name;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_triggerType").enumValueIndex = (int)ComboApexTriggerType.Passive;
            so.FindProperty("_effectType").enumValueIndex = (int)effect;
            so.FindProperty("_primingApex").objectReferenceValue = primer;
            so.FindProperty("_consumingApex").objectReferenceValue = consumer;
            so.FindProperty("_primeWindowSeconds").floatValue = primeWindow;
            so.FindProperty("_consumeDamageMultiplier").floatValue = Mathf.Max(1f, multiplier);
            so.FindProperty("_effectRadius").floatValue = effectRadius;
            so.FindProperty("_burnExtendSeconds").floatValue = burnExtend;
            so.FindProperty("_igniteBurnPerTick").floatValue = ignitePerTick;
            so.FindProperty("_igniteBurnDuration").floatValue = igniteDuration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return combo;
        }

        private static void WireBootstrap(GameSessionBootstrap bootstrap, ComboApexTalentDefinitionSO combo)
        {
            var so = new SerializedObject(bootstrap);
            var list = so.FindProperty("_comboApexes");
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == combo) present = true;
            if (!present)
            {
                list.arraySize += 1;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = combo;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ApexTalentDefinitionSO LoadApex(string fileNameNoExt) =>
            AssetDatabase.LoadAssetAtPath<ApexTalentDefinitionSO>($"{LineFolder}/{fileNameNoExt}.asset");

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
