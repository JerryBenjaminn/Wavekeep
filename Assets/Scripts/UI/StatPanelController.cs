using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Abilities;
using Wavekeep.Core;
using Wavekeep.Data;
using Wavekeep.Economy;
using Wavekeep.Runtime;

namespace Wavekeep.UI
{
    /// <summary>
    /// Toggleable in-game stat panel (Task 22): a player-facing transparency/debug view of the current
    /// run's effective ability stats, owned upgrades, active consumable effects, and reroll count.
    ///
    /// All ability numbers come from <see cref="HeroRuntime.BasicStats"/>/<see cref="HeroRuntime.UltimateStats"/>,
    /// which resolve through the SAME pipeline execution uses — the panel never re-derives stats (a
    /// reviewer-blocking requirement). It is rebuilt every frame while open, so values track live as
    /// upgrades/consumables/rerolls change. Read-only: it edits nothing.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Stat Panel")]
    public sealed class StatPanelController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("UI")]
        [Tooltip("Container toggled open/closed (this component's object stays active to read input).")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _contentText;

        [Header("Input")]
        [SerializeField] private Key _toggleKey = Key.Tab;

        private GameSession _session;
        private HeroRuntime _hero;
        private readonly StringBuilder _sb = new StringBuilder(512);

        private void Start()
        {
            _session = _bootstrap != null ? _bootstrap.Session : null;
            if (_session == null)
            {
                Debug.LogWarning("[StatPanelController] No GameSessionBootstrap/Session assigned; disabling.", this);
                enabled = false;
                return;
            }
            SetOpen(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[_toggleKey].wasPressedThisFrame)
            {
                SetOpen(_panel == null || !_panel.activeSelf);
            }

            if (_panel != null && _panel.activeSelf)
            {
                Refresh();
            }
        }

        private void SetOpen(bool open)
        {
            if (_panel != null) _panel.SetActive(open);
            if (open) Refresh();
        }

        private void Refresh()
        {
            if (_hero == null) _hero = Object.FindFirstObjectByType<HeroRuntime>();
            if (_contentText == null) return;

            _sb.Clear();
            _sb.AppendLine("<b>RUN STATS</b>  (Tab to close)");
            _sb.AppendLine();

            if (_hero != null)
            {
                _sb.Append("<b>Hero:</b> ").AppendLine(_hero.Definition != null ? _hero.Definition.HeroName : "—");
                // Task 24: live total Luck (hero base + equipped gear + in-run potions), read straight off
                // HeroRuntime — the same value the shop/loot rolls reweight against (no re-derivation here).
                _sb.Append("<b>Luck:</b> ").AppendLine(_hero.CurrentLuck.ToString("0"));
                AppendAbility("Basic", _hero.BasicStats);
                AppendAbility("Ultimate", _hero.UltimateStats);
            }
            else
            {
                _sb.AppendLine("<i>No hero spawned yet.</i>");
            }

            AppendCrit();
            AppendUpgrades();
            AppendConsumables();
            _sb.Append("<b>Reroll points:</b> ").Append(_session.RerollManager.CurrentPoints);

            _contentText.text = _sb.ToString();
        }

        private void AppendAbility(string role, AbilityStats? maybeStats)
        {
            _sb.AppendLine();
            if (maybeStats == null)
            {
                _sb.Append("<b>").Append(role).AppendLine(":</b> —");
                return;
            }

            var s = maybeStats.Value;
            string name = s.Definition != null ? s.Definition.AbilityName : "—";
            _sb.Append("<b>").Append(role).Append(":</b> ").AppendLine(name);

            // Zone ultimate shows its DoT payload; everything else shows hit damage.
            if (s.AppliesZonePayload)
            {
                _sb.Append("  DoT: ").Append(s.ZoneDotPerSecond.ToString("0.#")).Append("/s")
                   .Append("   Duration: ").Append(s.ZoneDuration.ToString("0.#")).Append("s")
                   .Append("   Slow: ").Append((s.ZoneSlowMagnitude * 100f).ToString("0")).AppendLine("%");
            }
            else
            {
                _sb.Append("  Damage: ").AppendLine(s.Damage.ToString("0.#"));
            }

            _sb.Append("  Cooldown: ").Append(s.Cooldown.ToString("0.##")).AppendLine("s");
            _sb.Append("  ").AppendLine(RadiusLine(s));

            if (s.AppliesFrostStack)
            {
                _sb.Append("  Frost: ").Append((s.FrostPerStackSlow * 100f).ToString("0")).Append("%/stack, max ")
                   .Append(s.FrostMaxStacks).Append(", freeze ").Append(s.FrostFreezeDuration.ToString("0.#")).AppendLine("s");
            }

            _sb.Append("  Charge: ").Append((s.CooldownProgress01 * 100f).ToString("0")).Append("%")
               .AppendLine(s.IsReady ? "  (READY)" : "");
        }

        private static string RadiusLine(AbilityStats s)
        {
            switch (s.TargetingType)
            {
                case AbilityTargetingType.TargetedAreaOfEffect:
                    return $"Blast radius: {s.Range:0.##}m  (cast {s.CastDistance:0.#}m)";
                case AbilityTargetingType.AreaOfEffect:
                    return $"AoE radius: {s.Range:0.##}m";
                default:
                    return $"Range: {s.Range:0.##}m";
            }
        }

        // Task 23: crit is a global combat modifier (from consumables), so show it once rather than per-ability.
        private void AppendCrit()
        {
            _sb.AppendLine();
            var c = _session.ConsumableInventory;
            _sb.Append("<b>Crit:</b> ").Append((c.TotalCritChance() * 100f).ToString("0")).Append("% chance, +")
               .Append((c.TotalCritDamageBonus() * 100f).ToString("0")).AppendLine("% dmg");
        }

        private void AppendUpgrades()
        {
            _sb.AppendLine();
            var upgrades = _session.UpgradeInventory.Upgrades;
            _sb.Append("<b>Upgrades (").Append(upgrades.Count).AppendLine("):</b>");
            if (upgrades.Count == 0)
            {
                _sb.AppendLine("  none");
                return;
            }
            for (int i = 0; i < upgrades.Count; i++)
            {
                var u = upgrades[i];
                if (u == null) continue;
                _sb.Append("  - ").Append(u.UpgradeName);
                AppendTags(u);
                _sb.AppendLine();
            }
        }

        private void AppendTags(UpgradeDefinitionSO upgrade)
        {
            var tags = upgrade.Tags;
            if (upgrade.Branch != UpgradeBranch.Neutral) _sb.Append(" [").Append(upgrade.Branch).Append(']');
            if (tags == null || tags.Count == 0) return;
            _sb.Append(" <size=80%>(");
            for (int i = 0; i < tags.Count; i++)
            {
                if (i > 0) _sb.Append(", ");
                _sb.Append(tags[i]);
            }
            _sb.Append(")</size>");
        }

        private void AppendConsumables()
        {
            _sb.AppendLine();
            var consumables = _session.ConsumableInventory;
            int count = consumables.ActiveEffectCount;
            _sb.Append("<b>Active consumables (").Append(count).AppendLine("):</b>");
            if (count == 0)
            {
                _sb.AppendLine("  none");
                return;
            }
            for (int i = 0; i < count; i++)
            {
                var e = consumables.GetActiveEffect(i);
                _sb.Append("  - ").Append(e.Type).Append(" ").Append(e.Value.ToString("0.##"));
                _sb.AppendLine(e.Permanent ? "  (permanent)" : $"  ({e.RemainingSeconds:0.#}s left)");
            }
        }
    }
}
