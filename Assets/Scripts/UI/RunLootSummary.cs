using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Gear;

namespace Wavekeep.UI
{
    /// <summary>
    /// Task 69: end-of-run loot summary. ADDITIVE to the existing <see cref="RunEndScreen"/> run-end flow — it is
    /// its own panel and does not touch or replace the victory/defeat stats. During the run it accumulates every
    /// <see cref="GearDroppedEvent"/> (the same drop event the arena VFX uses — no parallel drop tracking); when
    /// <see cref="RunEndedEvent"/> fires it renders the collected instances grouped by rarity (highest first),
    /// colour-coded via <see cref="RarityPalette"/>, and shows its panel. Pure view (CLAUDE.md §3.3) — it never
    /// touches gear state.
    ///
    /// Per-run lifetime is automatic: the gameplay scene (and this component) is rebuilt for each run, so the
    /// accumulated list starts empty every run with no cross-run leak.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Run Loot Summary")]
    public sealed class RunLootSummary : MonoBehaviour
    {
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [Tooltip("Root object shown when the run ends, hidden during play.")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _listText;

        private EventBus _events;
        private readonly List<GearInstance> _dropped = new List<GearInstance>();
        private bool _shown;

        private void Start()
        {
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogWarning("[RunLootSummary] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }

            _events = _bootstrap.Session.Events;
            _events.Subscribe<GearDroppedEvent>(OnGearDropped);
            _events.Subscribe<RunEndedEvent>(OnRunEnded);
            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.Unsubscribe<GearDroppedEvent>(OnGearDropped);
            _events.Unsubscribe<RunEndedEvent>(OnRunEnded);
        }

        private void OnGearDropped(GearDroppedEvent evt)
        {
            if (evt.Item != null) _dropped.Add(evt.Item);
        }

        private void OnRunEnded(RunEndedEvent evt)
        {
            if (_shown) return; // RunEndedEvent is single-shot, but guard against re-entry.
            _shown = true;

            if (_titleText != null) _titleText.text = $"Loot This Run ({_dropped.Count})";
            if (_listText != null) _listText.text = BuildList();
            SetPanelVisible(true);
        }

        // Group by rarity, highest tier first; colour each line by its rarity. Slot + rarity + base name is enough
        // for this task (richer per-item detail belongs to the later Hub UI overhaul).
        private string BuildList()
        {
            if (_dropped.Count == 0) return "No gear dropped this run.";

            var sb = new StringBuilder();
            var values = (Rarity[])Enum.GetValues(typeof(Rarity));
            for (int r = values.Length - 1; r >= 0; r--)
            {
                var rarity = values[r];
                bool headerWritten = false;
                for (int i = 0; i < _dropped.Count; i++)
                {
                    var item = _dropped[i];
                    if (item == null || item.Rarity != rarity) continue;
                    if (!headerWritten)
                    {
                        sb.Append($"<color=#{RarityPalette.Hex(rarity)}>");
                        headerWritten = true;
                    }
                    string name = item.Base != null && !string.IsNullOrEmpty(item.Base.DisplayName)
                        ? item.Base.DisplayName : item.Slot.ToString();
                    sb.Append($"[{rarity}] {item.Slot} — {name}\n");
                }
                if (headerWritten) sb.Append("</color>");
            }
            return sb.ToString();
        }

        private void SetPanelVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }
    }
}
