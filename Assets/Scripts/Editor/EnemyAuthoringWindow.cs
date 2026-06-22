#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 15 authoring tool: a guided <see cref="EditorWindow"/> for creating a fully-wired
    /// <see cref="EnemyDefinitionSO"/> (and, optionally, an inline <see cref="LootTableSO"/>) in one
    /// pass — no field-by-field Inspector work and no required follow-up edits.
    ///
    /// Design notes (documented decisions, see /docs/tools/content-authoring.md):
    /// - <b>No boss flag exists on EnemyDefinitionSO.</b> "Boss-ness" is decided by the WaveConfig that
    ///   references the enemy (Task 10/13), not by an enemy field. So the "Boss preset" here is purely a
    ///   convenience that defaults the prefab to the placeholder boss and seeds tankier stats; it writes
    ///   no nonexistent field. Making an enemy an actual boss = referencing it from a wave (wave-config
    ///   authoring is out of scope for this task).
    /// - <b>No tint field exists either</b> (enemies differ by prefab). Choosing a tint therefore
    ///   generates a tinted capsule prefab (clone of the placeholder + a new URP material) and assigns it
    ///   as the enemy's prefab — honoring "use the placeholder capsule + pick a tint" without an SO change.
    ///
    /// This is editor-only tooling; it changes no runtime gameplay code.
    /// </summary>
    public sealed class EnemyAuthoringWindow : EditorWindow
    {
        private const string EnemyFolder = "Assets/Data/Enemies";
        private const string LootFolder = "Assets/Data/Loot";
        private const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";
        private const string PlaceholderEnemyPath = EnemyPrefabFolder + "/PlaceholderEnemy.prefab";
        private const string PlaceholderBossPath = EnemyPrefabFolder + "/PlaceholderBoss.prefab";

        private enum PrefabMode { PlaceholderCapsule, Custom }
        private enum LootMode { None, ExistingTable, NewInlineTable }

        // --- Template (Duplicate & Modify) ---
        private EnemyDefinitionSO _template;

        // --- Form state ---
        private string _enemyName = "New Enemy";
        private bool _bossPreset;

        private PrefabMode _prefabMode = PrefabMode.PlaceholderCapsule;
        private GameObject _customPrefab;
        private bool _useTint;
        private Color _tint = new Color(0.8f, 0.3f, 0.3f);

        private float _maxHealth = 10f;
        private float _moveSpeed = 3f;
        private float _contactDamage = 5f;
        private int _currencyReward = 5;
        private int _xpReward = 5;

        private LootMode _lootMode = LootMode.None;
        private LootTableSO _existingLootTable;
        private float _inlineDropChance = 0.1f;
        private readonly List<LootRow> _inlineEntries = new List<LootRow>();

        private Vector2 _scroll;

        private sealed class LootRow { public LootItemSO Item; public int Weight = 1; }

        [MenuItem("Wavekeep/Tools/Enemy Authoring")]
        public static void Open()
        {
            var window = GetWindow<EnemyAuthoringWindow>("Enemy Authoring");
            window.minSize = new Vector2(420f, 520f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Enemy Authoring", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Creates a ready-to-use EnemyDefinitionSO in " + EnemyFolder +
                                    ". Inline loot tables land in " + LootFolder + ".", MessageType.None);

            DrawTemplateSection();
            EditorGUILayout.Space();

            _enemyName = EditorGUILayout.TextField("Enemy Name", _enemyName);

            EditorGUILayout.Space();
            DrawPrefabSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Base Stats", EditorStyles.boldLabel);
            if (DrawBossPresetToggle()) ApplyBossPreset();
            _maxHealth = EditorGUILayout.FloatField("Max Health", _maxHealth);
            _moveSpeed = EditorGUILayout.FloatField("Move Speed", _moveSpeed);
            _contactDamage = EditorGUILayout.FloatField("Contact Damage", _contactDamage);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rewards", EditorStyles.boldLabel);
            _currencyReward = EditorGUILayout.IntField("Currency Reward", _currencyReward);
            _xpReward = EditorGUILayout.IntField("XP Reward", _xpReward);

            EditorGUILayout.Space();
            DrawLootSection();

            EditorGUILayout.Space();
            DrawValidationAndCreate();

            EditorGUILayout.EndScrollView();
        }

        // --- Sections -----------------------------------------------------------------------------

        private void DrawTemplateSection()
        {
            EditorGUILayout.LabelField("Duplicate & Modify (optional)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _template = (EnemyDefinitionSO)EditorGUILayout.ObjectField("Template", _template, typeof(EnemyDefinitionSO), false);
                using (new EditorGUI.DisabledScope(_template == null))
                {
                    if (GUILayout.Button("Load", GUILayout.Width(60f))) LoadFromTemplate();
                }
            }
            EditorGUILayout.LabelField(" ", "Pick an enemy, press Load to pre-fill, tweak, then Create a NEW asset.", EditorStyles.miniLabel);
        }

        private void DrawPrefabSection()
        {
            EditorGUILayout.LabelField("Visual / Prefab", EditorStyles.boldLabel);
            _prefabMode = (PrefabMode)EditorGUILayout.EnumPopup("Prefab Mode", _prefabMode);

            if (_prefabMode == PrefabMode.Custom)
            {
                _customPrefab = (GameObject)EditorGUILayout.ObjectField("Custom Prefab", _customPrefab, typeof(GameObject), false);
            }
            else
            {
                EditorGUILayout.LabelField(" ", _bossPreset
                    ? "Uses the placeholder BOSS capsule."
                    : "Uses the shared placeholder capsule.", EditorStyles.miniLabel);
                _useTint = EditorGUILayout.Toggle("Tint Capsule", _useTint);
                if (_useTint)
                {
                    _tint = EditorGUILayout.ColorField("Tint Color", _tint);
                    EditorGUILayout.LabelField(" ", "Generates a tinted capsule prefab + material under " + EnemyPrefabFolder + ".", EditorStyles.miniLabel);
                }
            }
        }

        private bool DrawBossPresetToggle()
        {
            bool newValue = EditorGUILayout.Toggle(new GUIContent("Boss Preset",
                "Convenience only — seeds tankier stats + the boss placeholder prefab. EnemyDefinitionSO " +
                "has no boss field; an enemy becomes a boss by being referenced from a WaveConfig."), _bossPreset);
            bool turnedOn = newValue && !_bossPreset;
            _bossPreset = newValue;
            return turnedOn;
        }

        private void ApplyBossPreset()
        {
            // Seed tankier defaults (mirrors the hand-authored BossGrunt) without locking the designer in.
            _maxHealth = 400f;
            _moveSpeed = 1.5f;
            _contactDamage = 30f;
            _currencyReward = 50;
            _xpReward = 50;
        }

        private void DrawLootSection()
        {
            EditorGUILayout.LabelField("Loot Drops (Task 13)", EditorStyles.boldLabel);
            _lootMode = (LootMode)EditorGUILayout.EnumPopup("Loot Mode", _lootMode);

            switch (_lootMode)
            {
                case LootMode.ExistingTable:
                    _existingLootTable = (LootTableSO)EditorGUILayout.ObjectField("Loot Table", _existingLootTable, typeof(LootTableSO), false);
                    break;

                case LootMode.NewInlineTable:
                    _inlineDropChance = EditorGUILayout.Slider("Drop Chance", _inlineDropChance, 0f, 1f);
                    EditorGUILayout.LabelField("Entries (item + weight)");
                    for (int i = 0; i < _inlineEntries.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var row = _inlineEntries[i];
                            row.Item = (LootItemSO)EditorGUILayout.ObjectField(row.Item, typeof(LootItemSO), false);
                            row.Weight = EditorGUILayout.IntField(row.Weight, GUILayout.Width(60f));
                            if (GUILayout.Button("X", GUILayout.Width(24f))) { _inlineEntries.RemoveAt(i); i--; }
                        }
                    }
                    if (GUILayout.Button("+ Add Entry")) _inlineEntries.Add(new LootRow());
                    break;
            }
        }

        private void DrawValidationAndCreate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            Validate(errors, warnings);

            foreach (var w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
            foreach (var e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);

            using (new EditorGUI.DisabledScope(errors.Count > 0))
            {
                if (GUILayout.Button("Create Enemy Asset", GUILayout.Height(32f)))
                {
                    // Guard: an exception escaping mid-OnGUI would unbalance the GUILayout stack and spam
                    // "Invalid GUILayout state". Catch, log, and let the layout finish cleanly.
                    try { CreateAsset(); }
                    catch (System.Exception e) { Debug.LogError($"[EnemyAuthoring] Create failed: {e}"); }
                }
            }
        }

        // --- Logic --------------------------------------------------------------------------------

        private void Validate(List<string> errors, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(_enemyName)) errors.Add("Enemy name must not be empty.");
            if (_maxHealth <= 0f) errors.Add("Max Health must be greater than 0.");
            if (_moveSpeed < 0f) errors.Add("Move Speed must not be negative.");
            if (_contactDamage < 0f) errors.Add("Contact Damage must not be negative.");
            if (_currencyReward < 0) errors.Add("Currency Reward must not be negative.");
            if (_xpReward < 0) errors.Add("XP Reward must not be negative.");

            if (_prefabMode == PrefabMode.Custom && _customPrefab == null)
                errors.Add("Custom prefab mode selected but no prefab assigned.");

            if (_lootMode == LootMode.ExistingTable && _existingLootTable == null)
                warnings.Add("Loot mode is 'Existing Table' but no table is assigned — enemy will drop nothing.");

            if (_lootMode == LootMode.NewInlineTable)
            {
                int valid = 0;
                foreach (var row in _inlineEntries) if (row.Item != null && row.Weight > 0) valid++;
                if (_inlineDropChance > 0f && valid == 0)
                    warnings.Add("Drop chance > 0 but no valid loot entries (item + weight > 0) — nothing can drop.");
            }
        }

        private void LoadFromTemplate()
        {
            if (_template == null) return;
            _enemyName = _template.EnemyName + " Copy";
            _maxHealth = _template.MaxHealth;
            _moveSpeed = _template.MoveSpeed;
            _contactDamage = _template.ContactDamage;
            _currencyReward = _template.CurrencyReward;
            _xpReward = _template.XpReward;

            // Reuse the template's prefab directly (custom mode) so the duplicate looks identical.
            _prefabMode = PrefabMode.Custom;
            _customPrefab = _template.Prefab;
            _useTint = false;

            if (_template.LootTable != null)
            {
                _lootMode = LootMode.ExistingTable;
                _existingLootTable = _template.LootTable;
            }
            else
            {
                _lootMode = LootMode.None;
            }
            Repaint();
        }

        private void CreateAsset()
        {
            EnsureFolders();

            // 1) Resolve the loot table reference (existing or freshly authored inline).
            LootTableSO lootTable = ResolveLootTable();

            // 2) Resolve the prefab (custom > tinted-placeholder > plain placeholder/boss).
            GameObject prefab = ResolvePrefab();

            // 3) Create + populate the enemy asset.
            string path = AssetDatabase.GenerateUniqueAssetPath($"{EnemyFolder}/{SanitizeFileName(_enemyName)}.asset");
            var enemy = ScriptableObject.CreateInstance<EnemyDefinitionSO>();
            AssetDatabase.CreateAsset(enemy, path);

            var so = new SerializedObject(enemy);
            so.FindProperty("_enemyName").stringValue = _enemyName;
            so.FindProperty("_prefab").objectReferenceValue = prefab;
            so.FindProperty("_maxHealth").floatValue = _maxHealth;
            so.FindProperty("_moveSpeed").floatValue = _moveSpeed;
            so.FindProperty("_contactDamage").floatValue = _contactDamage;
            so.FindProperty("_currencyReward").intValue = _currencyReward;
            so.FindProperty("_xpReward").intValue = _xpReward;
            so.FindProperty("_lootTable").objectReferenceValue = lootTable;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = enemy;
            EditorGUIUtility.PingObject(enemy);
            Debug.Log($"[EnemyAuthoring] Created enemy '{_enemyName}' at {path}" +
                      (lootTable != null ? $" (loot: {lootTable.name})" : "") +
                      (prefab != null ? $" (prefab: {prefab.name})" : " (WARNING: no prefab)"));
        }

        private LootTableSO ResolveLootTable()
        {
            if (_lootMode == LootMode.ExistingTable) return _existingLootTable;
            if (_lootMode != LootMode.NewInlineTable) return null;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{LootFolder}/LootTable_{SanitizeFileName(_enemyName)}.asset");
            var table = ScriptableObject.CreateInstance<LootTableSO>();
            AssetDatabase.CreateAsset(table, path);

            var so = new SerializedObject(table);
            so.FindProperty("_dropChance").floatValue = _inlineDropChance;
            var entries = so.FindProperty("_entries");
            var valid = new List<LootRow>();
            foreach (var row in _inlineEntries) if (row.Item != null && row.Weight > 0) valid.Add(row);
            entries.arraySize = valid.Count;
            for (int i = 0; i < valid.Count; i++)
            {
                var el = entries.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_item").objectReferenceValue = valid[i].Item;
                el.FindPropertyRelative("_weight").intValue = valid[i].Weight;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private GameObject ResolvePrefab()
        {
            if (_prefabMode == PrefabMode.Custom) return _customPrefab;

            string basePrefabPath = _bossPreset ? PlaceholderBossPath : PlaceholderEnemyPath;
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePrefabPath);
            if (basePrefab == null)
            {
                Debug.LogError($"[EnemyAuthoring] Placeholder prefab not found at {basePrefabPath}. " +
                               "Run the Task 01/10 setups first, or use Custom prefab mode.");
                return null;
            }

            if (!_useTint) return basePrefab;
            return CreateTintedPrefab(basePrefab, _tint, SanitizeFileName(_enemyName));
        }

        // Clones the placeholder capsule into a new standalone prefab and tints its renderer via a new
        // URP material — enemies have no SO-level tint, so a distinct visual must live on a prefab.
        private static GameObject CreateTintedPrefab(GameObject basePrefab, Color tint, string baseName)
        {
            // Object.Instantiate on a prefab asset yields a DISCONNECTED clone (not a prefab instance),
            // so SaveAsPrefabAsset writes a standalone prefab with its own tinted material — no unpack
            // needed (calling UnpackPrefabInstance here would throw, since the clone isn't an instance).
            var instance = Object.Instantiate(basePrefab);
            instance.name = baseName;
            try
            {
                var renderer = instance.GetComponentInChildren<MeshRenderer>();
                if (renderer != null)
                {
                    var mat = renderer.sharedMaterial != null
                        ? new Material(renderer.sharedMaterial)   // keep the project's (URP) shader
                        : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    SetMaterialColor(mat, tint);

                    string matPath = AssetDatabase.GenerateUniqueAssetPath($"{EnemyPrefabFolder}/{baseName}_Mat.mat");
                    AssetDatabase.CreateAsset(mat, matPath);
                    renderer.sharedMaterial = mat;
                }
                else
                {
                    Debug.LogWarning("[EnemyAuthoring] Placeholder has no MeshRenderer; tint skipped.");
                }

                string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{EnemyPrefabFolder}/{baseName}.prefab");
                return PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void SetMaterialColor(Material mat, Color c)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); // URP/Lit main color
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);         // built-in fallback
            mat.color = c;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Enemies");
            EnsureFolder("Assets/Data", "Loot");
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Enemies");
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unnamed";
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                // spaces/punctuation dropped → PascalCase-ish file name matching existing assets
            }
            return sb.Length > 0 ? sb.ToString() : "Unnamed";
        }
    }
}
#endif
