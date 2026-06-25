using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Progression;

namespace Wavekeep.UI
{
    /// <summary>
    /// Task 43: the Hub "Codex" screen. Lists EVERY apex and combo apex that exists in the project (from the
    /// authored <see cref="TalentCatalogSO"/> — never a hardcoded entry list, so new talents appear once they
    /// exist + are discovered), showing full detail for talents the player has DISCOVERED and a bare "???" for
    /// the rest. Discovery state comes from the persistent <see cref="TalentDiscoveryManager"/> on the session,
    /// which the Hub's bootstrap reloads from disk on scene entry — so a talent discovered in the previous run
    /// shows here immediately on return, no restart (the list is also rebuilt every time the panel opens).
    ///
    /// Placeholder-tier UI built at runtime, like the other Wavekeep hub controllers. Open/close is a simple
    /// panel toggle; the panel starts hidden.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Codex Controller")]
    public sealed class CodexController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [Tooltip("Authored registry of all apex + combo apex talents. Populated by the Task 43 editor setup.")]
        [SerializeField] private TalentCatalogSO _catalog;

        [Header("UI")]
        [Tooltip("Root toggled on while the Codex is open (off by default / on close).")]
        [SerializeField] private GameObject _panel;
        [Tooltip("Scroll content the talent rows are generated into.")]
        [SerializeField] private RectTransform _entryContainer;
        [Tooltip("Button that opens the Codex.")]
        [SerializeField] private Button _openButton;
        [Tooltip("Button that closes the Codex.")]
        [SerializeField] private Button _closeButton;

        private TalentDiscoveryManager _discovery;

        private void Start()
        {
            var session = _bootstrap != null ? _bootstrap.Session : null;
            _discovery = session != null ? session.TalentDiscovery : null;

            if (_openButton != null) _openButton.onClick.AddListener(Open);
            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            Close();
        }

        /// <summary>Open the Codex and (re)build its entries from the current discovery state.</summary>
        public void Open()
        {
            RebuildEntries();
            if (_panel != null) _panel.SetActive(true);
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void RebuildEntries()
        {
            if (_entryContainer == null) return;
            ClearChildren(_entryContainer);

            if (_catalog == null)
            {
                CreateRow("<color=#E0706F>No TalentCatalog wired — run 'Wavekeep/Setup Task 43 (Codex)'.</color>");
                return;
            }

            CreateHeader("APEX TALENTS");
            var apexes = _catalog.ApexTalents;
            int apexCount = apexes != null ? apexes.Count : 0;
            if (apexCount == 0) CreateRow("<color=#888888>(No apex talents authored yet)</color>");
            for (int i = 0; i < apexCount; i++)
            {
                var apex = apexes[i];
                if (apex == null) continue;
                bool known = _discovery != null && _discovery.IsDiscovered(apex);
                CreateRow(known ? DescribeApex(apex) : UndiscoveredText());
            }

            CreateHeader("COMBO APEX TALENTS");
            var combos = _catalog.ComboApexes;
            int comboCount = combos != null ? combos.Count : 0;
            if (comboCount == 0) CreateRow("<color=#888888>(No combo apexes authored yet)</color>");
            for (int i = 0; i < comboCount; i++)
            {
                var combo = combos[i];
                if (combo == null) continue;
                bool known = _discovery != null && _discovery.IsDiscovered(combo);
                CreateRow(known ? DescribeCombo(combo) : UndiscoveredText());
            }
        }

        // --- Entry text ---------------------------------------------------------------------------

        private static string UndiscoveredText() =>
            "<b><color=#888888>???</color></b>  <size=80%><color=#666666>(undiscovered)</color></size>";

        private static string DescribeApex(ApexTalentDefinitionSO apex)
        {
            var sb = new StringBuilder();
            string heroName = apex.Hero != null ? apex.Hero.HeroName : "Unknown Hero";
            sb.Append($"<b>{Safe(apex.ApexName, apex.name)}</b>  <size=75%><color=#9FB6E0>({heroName})</color></size>");
            if (!string.IsNullOrEmpty(apex.Description))
                sb.Append($"\n<size=85%>{apex.Description}</size>");
            sb.Append($"\n<size=80%><color=#C8A04C>Unlock: {ApexRequirement(apex)}</color></size>");
            return sb.ToString();
        }

        private static string DescribeCombo(ComboApexTalentDefinitionSO combo)
        {
            var sb = new StringBuilder();
            sb.Append($"<b>{Safe(combo.ComboName, combo.name)}</b>  <size=75%><color=#9FB6E0>(combo)</color></size>");
            if (!string.IsNullOrEmpty(combo.Description))
                sb.Append($"\n<size=85%>{combo.Description}</size>");
            sb.Append($"\n<size=80%><color=#C8A04C>Unlock: {ComboRequirement(combo)}</color></size>");
            return sb.ToString();
        }

        // "Frozen Ground Tier 3 + Deepening Frost Tier 3" — every required line maxed.
        private static string ApexRequirement(ApexTalentDefinitionSO apex)
        {
            var lines = apex.RequiredLines;
            if (lines == null || lines.Count == 0) return "—";
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) continue;
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append($"{line.LineName} Tier {line.TierCount}");
            }
            return sb.Length > 0 ? sb.ToString() : "—";
        }

        // "Remorseless Winter + Lethal Surge" — both prerequisite apexes unlocked.
        private static string ComboRequirement(ComboApexTalentDefinitionSO combo)
        {
            string a = combo.PrimingApex != null ? Safe(combo.PrimingApex.ApexName, combo.PrimingApex.name) : "?";
            string b = combo.ConsumingApex != null ? Safe(combo.ConsumingApex.ApexName, combo.ConsumingApex.name) : "?";
            return $"{a} + {b}";
        }

        private static string Safe(string primary, string fallback) =>
            !string.IsNullOrEmpty(primary) ? primary : fallback;

        // --- UI helpers ---------------------------------------------------------------------------

        private void CreateHeader(string text)
        {
            var tmp = CreateText($"<b>{text}</b>", 24f, new Color(0.85f, 0.85f, 0.95f));
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(720f, 34f);
        }

        private void CreateRow(string richText)
        {
            var go = new GameObject("CodexRow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_entryContainer, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(720f, 96f);
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);

            var tmp = CreateText(richText, 19f, Color.white);
            tmp.transform.SetParent(go.transform, false);
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 6f); lrt.offsetMax = new Vector2(-10f, -6f);
            tmp.alignment = TextAlignmentOptions.TopLeft;
        }

        private TextMeshProUGUI CreateText(string text, float fontSize, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_entryContainer, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.richText = true;
            tmp.alignment = TextAlignmentOptions.Left;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            ((RectTransform)tmp.transform).sizeDelta = new Vector2(720f, fontSize + 8f);
            return tmp;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
