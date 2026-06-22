#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 16 authoring tool: a guided <see cref="EditorWindow"/> for viewing and editing a
    /// <see cref="DifficultyTierSO"/>'s wave sequence and each <see cref="WaveConfigSO"/>'s composition
    /// without hand-navigating nested list fields in the default Inspector.
    ///
    /// CRITICAL (acceptance + reviewer note): this is a VIEW/EDITOR over the real assets, never a parallel
    /// in-memory copy. Every field is drawn/edited through a live <see cref="SerializedObject"/> wrapping
    /// the actual SO (so edits are undoable, real writes), and structural list ops go through
    /// SerializedProperty array operations + <see cref="EditorUtility.SetDirty"/> + AssetDatabase.SaveAssets.
    ///
    /// Documented decisions (see /docs/tools/content-authoring.md):
    /// - <b>Boss designation is POSITIONAL.</b> The runtime spawner uses list-index+1 as the wave number
    ///   (WaveSpawner: <c>_currentWaveNumber = waveIndex + 1</c>) and asks the TIER
    ///   <c>IsBossWave(waveNumber)</c>. So a wave is a boss wave when its 1-based position is a multiple of
    ///   the tier's BossWaveInterval — NOT based on the wave's own <c>WaveNumber</c> field (a display label).
    ///   The boss enemy + count are TIER-level fields (shared by all boss waves); only the
    ///   <c>bossLootTable</c> is per-wave (Task 13). The tool surfaces tier boss config in the tier header
    ///   and the per-wave boss loot table in the wave detail.
    /// - <b>Remove unlinks, it does not delete the asset file</b> (non-destructive) — the .asset stays on
    ///   disk and can be re-added or deleted manually.
    ///
    /// Editor-only tooling; it changes no runtime gameplay code.
    /// </summary>
    public sealed class WaveCompositionWindow : EditorWindow
    {
        private const string WaveFolder = "Assets/Data/Waves";

        private DifficultyTierSO _tier;
        private SerializedObject _tierSO;

        private WaveConfigSO _selectedWave;     // the object currently wrapped by _waveSO
        private SerializedObject _waveSO;
        private int _selectedIndex = -1;

        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        // Deferred structural action, executed after the layout pass to avoid mutating arrays mid-draw.
        private System.Action _deferred;

        [MenuItem("Wavekeep/Tools/Wave Composition")]
        public static void Open()
        {
            var window = GetWindow<WaveCompositionWindow>("Wave Composition");
            window.minSize = new Vector2(720f, 520f);
        }

        private void OnGUI()
        {
            _deferred = null;

            DrawTierPicker();
            if (_tier == null)
            {
                EditorGUILayout.HelpBox("Pick a DifficultyTierSO to view and edit its waves.", MessageType.Info);
                return;
            }

            EnsureSerializedTier();
            _tierSO.Update();

            DrawTierHeader();
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(300f)))
                    DrawWaveList();

                using (new EditorGUILayout.VerticalScope())
                    DrawWaveDetail();
            }

            // Apply staged field edits (header + selected wave) before any structural change runs.
            if (_waveSO != null) _waveSO.ApplyModifiedProperties();
            _tierSO.ApplyModifiedProperties();

            // Run a queued structural op (add/remove/reorder). It persists + may ExitGUI internally.
            if (_deferred != null)
            {
                var action = _deferred;
                _deferred = null;
                try { action(); }
                catch (ExitGUIException) { throw; }
                catch (System.Exception e) { Debug.LogError($"[WaveComposition] Action failed: {e}"); }
            }
        }

        // --- Tier picker + header -----------------------------------------------------------------

        private void DrawTierPicker()
        {
            EditorGUILayout.LabelField("Wave Composition", EditorStyles.boldLabel);
            var picked = (DifficultyTierSO)EditorGUILayout.ObjectField("Difficulty Tier", _tier, typeof(DifficultyTierSO), false);
            if (picked != _tier)
            {
                _tier = picked;
                _tierSO = null;
                _selectedIndex = -1;
                _selectedWave = null;
                _waveSO = null;
            }
        }

        private void DrawTierHeader()
        {
            EditorGUILayout.LabelField("Tier Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_tierSO.FindProperty("_tierName"));
            EditorGUILayout.PropertyField(_tierSO.FindProperty("_globalStatMultiplier"));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(_tierSO.FindProperty("_milestoneWaveInterval"), GUILayout.MinWidth(120f));
                EditorGUILayout.PropertyField(_tierSO.FindProperty("_milestoneStep"), GUILayout.MinWidth(120f));
            }

            EditorGUILayout.LabelField("Boss Waves (tier-level; loot table is per-wave)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_tierSO.FindProperty("_bossWaveInterval"));
            EditorGUILayout.PropertyField(_tierSO.FindProperty("_bossDefinition"));
            EditorGUILayout.PropertyField(_tierSO.FindProperty("_bossCount"));

            int bossInterval = _tierSO.FindProperty("_bossWaveInterval").intValue;
            var bossDef = _tierSO.FindProperty("_bossDefinition").objectReferenceValue;
            if (bossInterval > 0 && bossDef == null)
                EditorGUILayout.HelpBox("Boss wave interval is set but no Boss Definition is assigned — " +
                                        "no boss will actually spawn (runtime IsBossWave requires a boss).", MessageType.Warning);
        }

        // --- Wave list (left) ---------------------------------------------------------------------

        private void DrawWaveList()
        {
            var wavesProp = _tierSO.FindProperty("_waves");
            int bossInterval = _tierSO.FindProperty("_bossWaveInterval").intValue;
            bool hasBossDef = _tierSO.FindProperty("_bossDefinition").objectReferenceValue != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Waves ({wavesProp.arraySize})", EditorStyles.boldLabel);
                if (GUILayout.Button("+ Add Wave", GUILayout.Width(90f)))
                    _deferred = AddWave;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll, GUILayout.ExpandHeight(true)))
            {
                _listScroll = scroll.scrollPosition;

                for (int i = 0; i < wavesProp.arraySize; i++)
                {
                    var wave = wavesProp.GetArrayElementAtIndex(i).objectReferenceValue as WaveConfigSO;
                    bool isBossPos = bossInterval > 0 && ((i + 1) % bossInterval == 0);

                    bool selected = i == _selectedIndex;
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            int captured = i;
                            string label = BuildWaveRowLabel(i, wave, isBossPos, hasBossDef);
                            if (GUILayout.Button(label, selected ? EditorStyles.whiteLabel : EditorStyles.label))
                                _deferred = () => SelectWave(captured);

                            using (new EditorGUI.DisabledScope(i == 0))
                                if (GUILayout.Button("▲", GUILayout.Width(26f))) _deferred = () => MoveWave(captured, captured - 1);
                            using (new EditorGUI.DisabledScope(i == wavesProp.arraySize - 1))
                                if (GUILayout.Button("▼", GUILayout.Width(26f))) _deferred = () => MoveWave(captured, captured + 1);
                            if (GUILayout.Button("✕", GUILayout.Width(26f))) _deferred = () => RemoveWave(captured);
                        }
                    }
                }
            }
        }

        private static string BuildWaveRowLabel(int index, WaveConfigSO wave, bool isBossPos, bool hasBossDef)
        {
            var sb = new StringBuilder();
            sb.Append($"#{index + 1}");
            if (wave == null) { sb.Append("  <null wave ref>"); return sb.ToString(); }

            int totalEnemies = 0;
            foreach (var e in wave.SpawnEntries) if (e != null) totalEnemies += Mathf.Max(0, e.Count);
            sb.Append($"  ({wave.SpawnEntries.Count} type/s, {totalEnemies} enemies)");
            if (isBossPos) sb.Append(hasBossDef ? "  [BOSS]" : "  [BOSS?]");
            return sb.ToString();
        }

        private void SelectWave(int index)
        {
            _selectedIndex = index;
            var wave = (index >= 0 && index < _tier.Waves.Count) ? _tier.Waves[index] : null;
            EnsureSerializedWave(wave);
            Repaint();
        }

        // --- Wave detail (right) ------------------------------------------------------------------

        private void DrawWaveDetail()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _tier.Waves.Count)
            {
                EditorGUILayout.HelpBox("Select a wave from the list to edit it.", MessageType.Info);
                return;
            }

            var wave = _tier.Waves[_selectedIndex];
            if (wave == null)
            {
                EditorGUILayout.HelpBox("This slot has a null WaveConfigSO reference. Remove it or fix the tier list.", MessageType.Warning);
                return;
            }

            EnsureSerializedWave(wave);
            _waveSO.Update();

            int bossInterval = _tierSO.FindProperty("_bossWaveInterval").intValue;
            bool hasBossDef = _tierSO.FindProperty("_bossDefinition").objectReferenceValue != null;
            bool isBossPos = bossInterval > 0 && ((_selectedIndex + 1) % bossInterval == 0);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_detailScroll))
            {
                _detailScroll = scroll.scrollPosition;

                EditorGUILayout.LabelField($"Wave #{_selectedIndex + 1} — {wave.name}", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_waveSO.FindProperty("_waveNumber"),
                    new GUIContent("Wave Number (display label only)"));
                EditorGUILayout.PropertyField(_waveSO.FindProperty("_statMultiplier"),
                    new GUIContent("Stat Multiplier (per-wave)"));

                EditorGUILayout.Space();
                DrawSpawnEntries();

                EditorGUILayout.Space();
                DrawBossSection(isBossPos, hasBossDef);

                EditorGUILayout.Space();
                DrawWaveValidation(wave, isBossPos, hasBossDef);
            }
        }

        private void DrawSpawnEntries()
        {
            var entries = _waveSO.FindProperty("_spawnEntries");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Spawn Entries", EditorStyles.boldLabel);
                if (GUILayout.Button("+ Add Entry", GUILayout.Width(90f)))
                    _deferred = AddSpawnEntry;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Enemy", GUILayout.MinWidth(140f));
                EditorGUILayout.LabelField("Count", GUILayout.Width(70f));
                EditorGUILayout.LabelField("Interval", GUILayout.Width(70f));
                GUILayout.Space(30f);
            }

            for (int i = 0; i < entries.arraySize; i++)
            {
                var element = entries.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("_enemyType"), GUIContent.none, GUILayout.MinWidth(140f));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("_count"), GUIContent.none, GUILayout.Width(70f));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("_spawnInterval"), GUIContent.none, GUILayout.Width(70f));
                    int captured = i;
                    if (GUILayout.Button("✕", GUILayout.Width(26f))) _deferred = () => RemoveSpawnEntry(captured);
                }
            }
        }

        private void DrawBossSection(bool isBossPos, bool hasBossDef)
        {
            EditorGUILayout.LabelField("Boss (this wave)", EditorStyles.boldLabel);
            if (!isBossPos)
            {
                EditorGUILayout.LabelField($"Not a boss position (boss every {_tierSO.FindProperty("_bossWaveInterval").intValue} waves).",
                    EditorStyles.miniLabel);
                return;
            }

            var bossDefProp = _tierSO.FindProperty("_bossDefinition");
            EditorGUILayout.LabelField($"Boss enemy (tier-level): {(hasBossDef ? bossDefProp.objectReferenceValue.name : "<none>")}", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(_waveSO.FindProperty("_bossLootTable"), new GUIContent("Boss Loot Table (this wave)"));
        }

        private void DrawWaveValidation(WaveConfigSO wave, bool isBossPos, bool hasBossDef)
        {
            bool spawnsAnyEnemy = false;
            bool hasNullEnemy = false;
            foreach (var e in wave.SpawnEntries)
            {
                if (e == null) continue;
                if (e.EnemyType == null) hasNullEnemy = true;
                else if (e.Count > 0) spawnsAnyEnemy = true;
            }

            bool effectiveBoss = isBossPos && hasBossDef;
            if (!spawnsAnyEnemy && !effectiveBoss)
                EditorGUILayout.HelpBox("This wave spawns no enemies and is not an effective boss wave — it will complete instantly.", MessageType.Warning);
            if (hasNullEnemy)
                EditorGUILayout.HelpBox("A spawn entry has no EnemyDefinitionSO assigned — it will spawn nothing.", MessageType.Warning);
            if (isBossPos && hasBossDef && wave.BossLootTable == null)
                EditorGUILayout.HelpBox("This is a boss wave but has no Boss Loot Table — the boss will drop nothing.", MessageType.Warning);
        }

        // --- Structural operations (persisted) ----------------------------------------------------

        private void AddWave()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Waves");

            var prev = _tier.Waves.Count > 0 ? _tier.Waves[_tier.Waves.Count - 1] : null;

            string baseName = $"Wave_{SanitizeFileName(_tier.TierName)}_{_tier.Waves.Count + 1:00}";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{WaveFolder}/{baseName}.asset");
            var newWave = ScriptableObject.CreateInstance<WaveConfigSO>();
            AssetDatabase.CreateAsset(newWave, path);

            var nso = new SerializedObject(newWave);
            if (prev != null)
            {
                // Duplicate-from-previous (mirrors Task 15's flow): copy composition + scaling field-by-
                // field (NOT EditorUtility.CopySerialized, which would also clobber the asset's name).
                // Boss loot table is deliberately left null — a new appended wave is usually not a boss
                // position; the designer sets it explicitly if it is.
                var prevSO = new SerializedObject(prev);
                nso.FindProperty("_waveNumber").intValue = prev.WaveNumber + 1;
                nso.FindProperty("_statMultiplier").floatValue = prev.StatMultiplier;

                var src = prevSO.FindProperty("_spawnEntries");
                var dst = nso.FindProperty("_spawnEntries");
                dst.arraySize = src.arraySize;
                for (int i = 0; i < src.arraySize; i++)
                {
                    var s = src.GetArrayElementAtIndex(i);
                    var d = dst.GetArrayElementAtIndex(i);
                    d.FindPropertyRelative("_enemyType").objectReferenceValue = s.FindPropertyRelative("_enemyType").objectReferenceValue;
                    d.FindPropertyRelative("_count").intValue = s.FindPropertyRelative("_count").intValue;
                    d.FindPropertyRelative("_spawnInterval").floatValue = s.FindPropertyRelative("_spawnInterval").floatValue;
                }
            }
            else
            {
                nso.FindProperty("_waveNumber").intValue = 1;
            }
            nso.ApplyModifiedPropertiesWithoutUndo();

            _tierSO.Update();
            var wavesProp = _tierSO.FindProperty("_waves");
            int idx = wavesProp.arraySize;
            wavesProp.InsertArrayElementAtIndex(idx);
            wavesProp.GetArrayElementAtIndex(idx).objectReferenceValue = newWave;
            _tierSO.ApplyModifiedProperties();

            EditorUtility.SetDirty(newWave);
            EditorUtility.SetDirty(_tier);
            AssetDatabase.SaveAssets();

            SelectWave(idx);
            Repaint();
        }

        private void MoveWave(int from, int to)
        {
            _tierSO.Update();
            var wavesProp = _tierSO.FindProperty("_waves");
            if (to < 0 || to >= wavesProp.arraySize) return;
            wavesProp.MoveArrayElement(from, to);
            _tierSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_tier);
            AssetDatabase.SaveAssets();

            if (_selectedIndex == from) _selectedIndex = to;
            else if (_selectedIndex == to) _selectedIndex = from;
            Repaint();
        }

        private void RemoveWave(int index)
        {
            // Unlink only (non-destructive): remove the reference from the tier list; the .asset stays.
            _tierSO.Update();
            var wavesProp = _tierSO.FindProperty("_waves");
            RemoveObjectArrayElement(wavesProp, index);
            _tierSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_tier);
            AssetDatabase.SaveAssets();

            if (_selectedIndex == index) { _selectedIndex = -1; _selectedWave = null; _waveSO = null; }
            else if (_selectedIndex > index) _selectedIndex--;
            Repaint();
        }

        private void AddSpawnEntry()
        {
            if (_waveSO == null) return;
            _waveSO.Update();
            var entries = _waveSO.FindProperty("_spawnEntries");
            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            // Reset the freshly-inserted element to clean defaults (Insert copies the previous element).
            var el = entries.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("_enemyType").objectReferenceValue = null;
            el.FindPropertyRelative("_count").intValue = 1;
            el.FindPropertyRelative("_spawnInterval").floatValue = 0.5f;
            _waveSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedWave);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        private void RemoveSpawnEntry(int index)
        {
            if (_waveSO == null) return;
            _waveSO.Update();
            var entries = _waveSO.FindProperty("_spawnEntries");
            if (index < 0 || index >= entries.arraySize) return;
            entries.DeleteArrayElementAtIndex(index); // plain struct list: single delete removes the slot
            _waveSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(_selectedWave);
            AssetDatabase.SaveAssets();
            Repaint();
        }

        // --- Helpers ------------------------------------------------------------------------------

        private void EnsureSerializedTier()
        {
            if (_tierSO == null || _tierSO.targetObject != _tier)
                _tierSO = new SerializedObject(_tier);
        }

        private void EnsureSerializedWave(WaveConfigSO wave)
        {
            if (wave == null) { _selectedWave = null; _waveSO = null; return; }
            if (_waveSO == null || _selectedWave != wave)
            {
                _selectedWave = wave;
                _waveSO = new SerializedObject(wave);
            }
        }

        // Object-reference arrays need the classic two-step delete: the first DeleteArrayElementAtIndex on
        // a non-null element only nulls it; a second call removes the slot.
        private static void RemoveObjectArrayElement(SerializedProperty arrayProp, int index)
        {
            if (index < 0 || index >= arrayProp.arraySize) return;
            if (arrayProp.GetArrayElementAtIndex(index).objectReferenceValue != null)
                arrayProp.DeleteArrayElementAtIndex(index);
            if (index < arrayProp.arraySize)
                arrayProp.DeleteArrayElementAtIndex(index);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Tier";
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.Length > 0 ? sb.ToString() : "Tier";
        }
    }
}
#endif
