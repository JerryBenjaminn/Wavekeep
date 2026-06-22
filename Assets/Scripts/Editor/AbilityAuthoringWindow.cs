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
    /// <see cref="AbilityDefinitionSO"/> or <see cref="UpgradeDefinitionSO"/> in one pass. A top toggle
    /// switches mode and shows only the fields that the chosen SO type actually has (so an upgrade can
    /// never be given ability-only fields, and vice-versa — a §29/locked-rule safety requirement).
    ///
    /// Hero-exclusive upgrades (CLAUDE.md §3.8): when "Hero-Exclusive" is ticked and a hero is chosen,
    /// Create both authors the <see cref="UpgradeDefinitionSO"/> AND appends it to that
    /// <see cref="HeroDefinitionSO"/>'s <c>_exclusiveUpgrades</c> list — no separate manual step.
    ///
    /// Editor-only tooling; changes no runtime gameplay code.
    /// </summary>
    public sealed class AbilityAuthoringWindow : EditorWindow
    {
        private const string AbilityFolder = "Assets/Data/Abilities";
        private const string UpgradeFolder = "Assets/Data/Upgrades";

        private enum Mode { Ability, Upgrade }

        private Mode _mode = Mode.Ability;
        private Vector2 _scroll;

        // --- Templates (Duplicate & Modify) ---
        private AbilityDefinitionSO _abilityTemplate;
        private UpgradeDefinitionSO _upgradeTemplate;

        // --- Shared ---
        private string _name = "New Ability";
        private Sprite _icon;

        // --- Ability fields ---
        private float _baseDamage = 5f;
        private float _baseCooldown = 1f;
        private float _range = 10f;
        private AbilityTargetingType _targetingType = AbilityTargetingType.SingleTarget;
        private bool _appliesStatusEffects;
        private readonly List<LevelRow> _levels = new List<LevelRow>();
        private readonly List<RuleRow> _rules = new List<RuleRow>();

        // --- Upgrade fields ---
        private readonly List<UpgradeTag> _tags = new List<UpgradeTag>();
        private UpgradeEffectType _effectType = UpgradeEffectType.FlatDamageBonus;
        private float _effectValue;
        private bool _appliesStatusEffect;
        private StatusEffectType _statusType = StatusEffectType.Freeze;
        private float _statusMagnitude;
        private float _statusDuration;
        private bool _heroExclusive;
        private HeroDefinitionSO _exclusiveHero;

        private sealed class LevelRow { public float Damage = 1f; public float Cooldown = 1f; public float Range = 1f; }
        private sealed class RuleRow
        {
            public UpgradeTag Tag;
            public AbilityModifierType Type = AbilityModifierType.DamageMultiplier;
            public float Value = 1f;
        }

        [MenuItem("Wavekeep/Tools/Ability Authoring")]
        public static void Open()
        {
            var window = GetWindow<AbilityAuthoringWindow>("Ability Authoring");
            window.minSize = new Vector2(440f, 560f);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Ability / Upgrade Authoring", EditorStyles.boldLabel);
            var newMode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Ability", "Upgrade" });
            if (newMode != _mode)
            {
                _mode = newMode;
                // Reset the name placeholder so it's obvious which type you're making.
                if (_mode == Mode.Ability && _name == "New Upgrade") _name = "New Ability";
                if (_mode == Mode.Upgrade && _name == "New Ability") _name = "New Upgrade";
            }

            EditorGUILayout.Space();
            DrawTemplateSection();
            EditorGUILayout.Space();

            _name = EditorGUILayout.TextField(_mode == Mode.Ability ? "Ability Name" : "Upgrade Name", _name);
            _icon = (Sprite)EditorGUILayout.ObjectField("Icon (optional)", _icon, typeof(Sprite), false);

            EditorGUILayout.Space();
            if (_mode == Mode.Ability) DrawAbilityFields();
            else DrawUpgradeFields();

            EditorGUILayout.Space();
            DrawValidationAndCreate();

            EditorGUILayout.EndScrollView();
        }

        // --- Template section ----------------------------------------------------------------------

        private void DrawTemplateSection()
        {
            EditorGUILayout.LabelField("Duplicate & Modify (optional)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_mode == Mode.Ability)
                {
                    _abilityTemplate = (AbilityDefinitionSO)EditorGUILayout.ObjectField("Template", _abilityTemplate, typeof(AbilityDefinitionSO), false);
                    using (new EditorGUI.DisabledScope(_abilityTemplate == null))
                        if (GUILayout.Button("Load", GUILayout.Width(60f))) LoadAbilityTemplate();
                }
                else
                {
                    _upgradeTemplate = (UpgradeDefinitionSO)EditorGUILayout.ObjectField("Template", _upgradeTemplate, typeof(UpgradeDefinitionSO), false);
                    using (new EditorGUI.DisabledScope(_upgradeTemplate == null))
                        if (GUILayout.Button("Load", GUILayout.Width(60f))) LoadUpgradeTemplate();
                }
            }
            EditorGUILayout.LabelField(" ", "Load pre-fills the form; Create always makes a NEW asset.", EditorStyles.miniLabel);
        }

        // --- Ability mode --------------------------------------------------------------------------

        private void DrawAbilityFields()
        {
            EditorGUILayout.LabelField("Base Stats", EditorStyles.boldLabel);
            _baseDamage = EditorGUILayout.FloatField("Base Damage", _baseDamage);
            _baseCooldown = EditorGUILayout.FloatField("Base Cooldown", _baseCooldown);
            _range = EditorGUILayout.FloatField("Range / AoE Radius", _range);
            _targetingType = (AbilityTargetingType)EditorGUILayout.EnumPopup("Targeting Type", _targetingType);
            _appliesStatusEffects = EditorGUILayout.Toggle(new GUIContent("Applies Status Effects",
                "Flag the deliberate payload ability (usually the ultimate), not a rapid auto-basic."), _appliesStatusEffects);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Upgrade Levels (multipliers on base; level 1 = row 1)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Dmg×", GUILayout.Width(60f));
                EditorGUILayout.LabelField("CD×", GUILayout.Width(60f));
                EditorGUILayout.LabelField("Rng×", GUILayout.Width(60f));
            }
            for (int i = 0; i < _levels.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var row = _levels[i];
                    EditorGUILayout.LabelField($"L{i + 1}", GUILayout.Width(28f));
                    row.Damage = EditorGUILayout.FloatField(row.Damage, GUILayout.Width(60f));
                    row.Cooldown = EditorGUILayout.FloatField(row.Cooldown, GUILayout.Width(60f));
                    row.Range = EditorGUILayout.FloatField(row.Range, GUILayout.Width(60f));
                    if (GUILayout.Button("X", GUILayout.Width(24f))) { _levels.RemoveAt(i); i--; }
                }
            }
            if (GUILayout.Button("+ Add Level")) _levels.Add(new LevelRow());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tag Interaction Rules (react to held upgrade tags)", EditorStyles.boldLabel);
            for (int i = 0; i < _rules.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var row = _rules[i];
                    row.Tag = (UpgradeTag)EditorGUILayout.EnumPopup(row.Tag, GUILayout.Width(120f));
                    row.Type = (AbilityModifierType)EditorGUILayout.EnumPopup(row.Type, GUILayout.Width(150f));
                    row.Value = EditorGUILayout.FloatField(row.Value, GUILayout.Width(60f));
                    if (GUILayout.Button("X", GUILayout.Width(24f))) { _rules.RemoveAt(i); i--; }
                }
            }
            if (GUILayout.Button("+ Add Rule")) _rules.Add(new RuleRow());
        }

        // --- Upgrade mode --------------------------------------------------------------------------

        private void DrawUpgradeFields()
        {
            EditorGUILayout.LabelField("Tags (hero abilities react to these via TagInteractionRule)", EditorStyles.boldLabel);
            for (int i = 0; i < _tags.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _tags[i] = (UpgradeTag)EditorGUILayout.EnumPopup(_tags[i], GUILayout.Width(160f));
                    if (GUILayout.Button("X", GUILayout.Width(24f))) { _tags.RemoveAt(i); i--; }
                }
            }
            if (GUILayout.Button("+ Add Tag")) _tags.Add(UpgradeTag.AoE);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generic Effect", EditorStyles.boldLabel);
            _effectType = (UpgradeEffectType)EditorGUILayout.EnumPopup("Effect Type", _effectType);
            _effectValue = EditorGUILayout.FloatField("Effect Value", _effectValue);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Status Effect on Hit (Task 11)", EditorStyles.boldLabel);
            _appliesStatusEffect = EditorGUILayout.Toggle("Applies Status Effect", _appliesStatusEffect);
            if (_appliesStatusEffect)
            {
                _statusType = (StatusEffectType)EditorGUILayout.EnumPopup("Status Type", _statusType);
                _statusMagnitude = EditorGUILayout.FloatField(new GUIContent("Magnitude",
                    "Freeze: unused. Slow: fraction [0..1]. Burn: damage per tick."), _statusMagnitude);
                _statusDuration = EditorGUILayout.FloatField("Duration (s)", _statusDuration);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hero-Exclusive Pool (§3.8)", EditorStyles.boldLabel);
            _heroExclusive = EditorGUILayout.Toggle("Hero-Exclusive", _heroExclusive);
            if (_heroExclusive)
            {
                _exclusiveHero = (HeroDefinitionSO)EditorGUILayout.ObjectField("Hero", _exclusiveHero, typeof(HeroDefinitionSO), false);
                EditorGUILayout.LabelField(" ", "On Create, this upgrade is appended to the hero's exclusiveUpgrades list.", EditorStyles.miniLabel);
            }
        }

        // --- Validation + Create -------------------------------------------------------------------

        private void DrawValidationAndCreate()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            Validate(errors, warnings);

            foreach (var w in warnings) EditorGUILayout.HelpBox(w, MessageType.Warning);
            foreach (var e in errors) EditorGUILayout.HelpBox(e, MessageType.Error);

            using (new EditorGUI.DisabledScope(errors.Count > 0))
            {
                string label = _mode == Mode.Ability ? "Create Ability Asset" : "Create Upgrade Asset";
                if (GUILayout.Button(label, GUILayout.Height(32f)))
                {
                    try { CreateAsset(); }
                    catch (System.Exception e) { Debug.LogError($"[AbilityAuthoring] Create failed: {e}"); }
                }
            }
        }

        private void Validate(List<string> errors, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(_name)) errors.Add("Name must not be empty.");

            if (_mode == Mode.Ability)
            {
                if (_baseDamage < 0f) errors.Add("Base Damage must not be negative.");
                if (_baseCooldown <= 0f) errors.Add("Base Cooldown must be greater than 0.");
                if (_range < 0f) errors.Add("Range must not be negative.");
                foreach (var l in _levels)
                    if (l.Damage < 0f || l.Cooldown < 0f || l.Range < 0f)
                    { warnings.Add("An upgrade level has a negative multiplier — usually unintended."); break; }
            }
            else
            {
                if (_tags.Count == 0)
                    warnings.Add("No tags set — hero abilities can't react to this upgrade via tag interactions.");
                if (_appliesStatusEffect && _statusDuration <= 0f)
                    warnings.Add("Status effect enabled but duration is 0 — it will expire instantly.");
                if (_heroExclusive && _exclusiveHero == null)
                    errors.Add("Hero-Exclusive is on but no hero is assigned to register the upgrade with.");
            }
        }

        // --- Template loading ----------------------------------------------------------------------

        private void LoadAbilityTemplate()
        {
            if (_abilityTemplate == null) return;
            _name = _abilityTemplate.AbilityName + " Copy";
            _icon = _abilityTemplate.Icon;
            _baseDamage = _abilityTemplate.BaseDamage;
            _baseCooldown = _abilityTemplate.BaseCooldown;
            _range = _abilityTemplate.Range;
            _targetingType = _abilityTemplate.TargetingType;
            _appliesStatusEffects = _abilityTemplate.AppliesStatusEffects;

            _levels.Clear();
            foreach (var lvl in _abilityTemplate.UpgradeLevels)
                _levels.Add(new LevelRow { Damage = lvl.DamageMultiplier, Cooldown = lvl.CooldownMultiplier, Range = lvl.RangeMultiplier });

            _rules.Clear();
            foreach (var rule in _abilityTemplate.TagInteractionRules)
                _rules.Add(new RuleRow { Tag = rule.MatchTag, Type = rule.ModifierType, Value = rule.ModifierValue });
            Repaint();
        }

        private void LoadUpgradeTemplate()
        {
            if (_upgradeTemplate == null) return;
            _name = _upgradeTemplate.UpgradeName + " Copy";
            _icon = _upgradeTemplate.Icon;
            _effectType = _upgradeTemplate.EffectType;
            _effectValue = _upgradeTemplate.EffectValue;
            _appliesStatusEffect = _upgradeTemplate.AppliesStatusEffect;
            _statusType = _upgradeTemplate.StatusEffectType;
            _statusMagnitude = _upgradeTemplate.StatusMagnitude;
            _statusDuration = _upgradeTemplate.StatusDuration;

            _tags.Clear();
            foreach (var tag in _upgradeTemplate.Tags) _tags.Add(tag);

            // Template loading does not pre-set hero-exclusive registration — that's an explicit choice
            // per new asset, so a duplicated upgrade isn't silently re-registered onto a hero.
            _heroExclusive = false;
            _exclusiveHero = null;
            Repaint();
        }

        // --- Creation ------------------------------------------------------------------------------

        private void CreateAsset()
        {
            if (_mode == Mode.Ability) CreateAbility();
            else CreateUpgrade();
        }

        private void CreateAbility()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Abilities");

            string path = AssetDatabase.GenerateUniqueAssetPath($"{AbilityFolder}/{SanitizeFileName(_name)}.asset");
            var ability = ScriptableObject.CreateInstance<AbilityDefinitionSO>();
            AssetDatabase.CreateAsset(ability, path);

            var so = new SerializedObject(ability);
            so.FindProperty("_abilityName").stringValue = _name;
            so.FindProperty("_icon").objectReferenceValue = _icon;
            so.FindProperty("_baseDamage").floatValue = _baseDamage;
            so.FindProperty("_baseCooldown").floatValue = _baseCooldown;
            so.FindProperty("_range").floatValue = _range;
            so.FindProperty("_targetingType").enumValueIndex = (int)_targetingType;
            so.FindProperty("_appliesStatusEffects").boolValue = _appliesStatusEffects;

            var levels = so.FindProperty("_upgradeLevels");
            levels.arraySize = _levels.Count;
            for (int i = 0; i < _levels.Count; i++)
            {
                var el = levels.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("_damageMultiplier").floatValue = _levels[i].Damage;
                el.FindPropertyRelative("_cooldownMultiplier").floatValue = _levels[i].Cooldown;
                el.FindPropertyRelative("_rangeMultiplier").floatValue = _levels[i].Range;
            }

            var rules = so.FindProperty("_tagInteractionRules");
            rules.arraySize = _rules.Count;
            for (int i = 0; i < _rules.Count; i++)
                AbilityAssetUtil.SetRule(rules.GetArrayElementAtIndex(i), _rules[i].Tag, _rules[i].Type, _rules[i].Value);

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = ability;
            EditorGUIUtility.PingObject(ability);
            Debug.Log($"[AbilityAuthoring] Created ability '{_name}' at {path} " +
                      $"({_targetingType}, {_levels.Count} level(s), {_rules.Count} rule(s)).");
        }

        private void CreateUpgrade()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Upgrades");

            string path = AssetDatabase.GenerateUniqueAssetPath($"{UpgradeFolder}/{SanitizeFileName(_name)}.asset");
            var upgrade = ScriptableObject.CreateInstance<UpgradeDefinitionSO>();
            AssetDatabase.CreateAsset(upgrade, path);

            var so = new SerializedObject(upgrade);
            so.FindProperty("_upgradeName").stringValue = _name;
            so.FindProperty("_icon").objectReferenceValue = _icon;

            var tags = so.FindProperty("_tags");
            tags.arraySize = _tags.Count;
            for (int i = 0; i < _tags.Count; i++) tags.GetArrayElementAtIndex(i).enumValueIndex = (int)_tags[i];

            so.FindProperty("_effectType").enumValueIndex = (int)_effectType;
            so.FindProperty("_effectValue").floatValue = _effectValue;
            so.FindProperty("_appliesStatusEffect").boolValue = _appliesStatusEffect;
            so.FindProperty("_statusEffectType").enumValueIndex = (int)_statusType;
            so.FindProperty("_statusMagnitude").floatValue = _statusMagnitude;
            so.FindProperty("_statusDuration").floatValue = _statusDuration;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Hero-exclusive registration: append to the hero's list via SerializedObject so the change
            // persists to the hero asset — no manual follow-up step (§3.8 / acceptance criterion).
            if (_heroExclusive && _exclusiveHero != null)
                RegisterExclusive(_exclusiveHero, upgrade);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = upgrade;
            EditorGUIUtility.PingObject(upgrade);
            Debug.Log($"[AbilityAuthoring] Created upgrade '{_name}' at {path} " +
                      $"({_tags.Count} tag(s))" +
                      (_heroExclusive && _exclusiveHero != null ? $", registered to hero '{_exclusiveHero.HeroName}'." : "."));
        }

        private static void RegisterExclusive(HeroDefinitionSO hero, UpgradeDefinitionSO upgrade)
        {
            var hso = new SerializedObject(hero);
            var list = hso.FindProperty("_exclusiveUpgrades");
            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = upgrade;
            hso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hero);
        }

        // --- Helpers -------------------------------------------------------------------------------

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}")) AssetDatabase.CreateFolder(parent, child);
        }

        private static string SanitizeFileName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unnamed";
            var sb = new StringBuilder(raw.Length);
            foreach (char ch in raw) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.Length > 0 ? sb.ToString() : "Unnamed";
        }
    }
}
#endif
