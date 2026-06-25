using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Task 38: a CROSS-HERO apex talent — distinct from the single-hero <see cref="ApexTalentDefinitionSO"/>
    /// (Task 29). It references exactly two single-hero apexes that belong to DIFFERENT heroes; the combo
    /// unlocks only when BOTH referenced apexes are independently unlocked (each per its own Task 29/35 line
    /// requirements) in the same run. Read-only at runtime (CLAUDE.md §3.5) — all live state lives in the
    /// runtime resolver (<c>ComboApexState</c>) and the per-enemy prime markers, never written back here.
    ///
    /// Generalised via <see cref="TriggerType"/> so future combo apexes can be Passive synergies OR Active
    /// independent abilities without introducing a new SO type:
    /// <list type="bullet">
    /// <item><b>Passive</b> (this task): no cooldown of its own. <see cref="PrimingApex"/>'s hit marks a target
    ///   "primed" for <see cref="PrimeWindowSeconds"/>; if <see cref="ConsumingApex"/> hits that target while it
    ///   is still primed, that hit's damage is multiplied by <see cref="ConsumeDamageMultiplier"/> and the prime
    ///   is consumed. This is Frozen Lightning's model (Remorseless Winter primes, Lethal Surge consumes).</item>
    /// <item><b>Active</b> (future): would run as a third independent ability with its own cooldown — no Active
    ///   combo is designed in Task 38; the value merely has to be authorable for a later task to use.</item>
    /// </list>
    ///
    /// The two apex references are swappable assets, NOT a hardcoded pair — pointing them at a different pair
    /// of apexes (from any two heroes) yields a different combo with zero code change.
    /// </summary>
    [CreateAssetMenu(fileName = "ComboApexTalent", menuName = "Wavekeep/Combo Apex Talent")]
    public sealed class ComboApexTalentDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _comboName;
        [Tooltip("Task 43: player-facing flavour/effect text shown in the Codex once this combo is discovered.")]
        [SerializeField, TextArea] private string _description;
        [SerializeField] private Sprite _icon;

        [Tooltip("How this combo takes effect once both referenced apexes are unlocked. Frozen Lightning = Passive.")]
        [SerializeField] private ComboApexTriggerType _triggerType = ComboApexTriggerType.Passive;

        [Header("Required Apexes (two, from DIFFERENT heroes — BOTH must be unlocked)")]
        [Tooltip("Passive: the apex whose hit PRIMES a target (Remorseless Winter). For an Active combo this is " +
                 "simply one of the two prerequisite apexes.")]
        [SerializeField] private ApexTalentDefinitionSO _primingApex;
        [Tooltip("Passive: the apex that CONSUMES a primed target for amplified damage (Lethal Surge). For an " +
                 "Active combo this is simply the other prerequisite apex.")]
        [SerializeField] private ApexTalentDefinitionSO _consumingApex;

        [Header("Passive Synergy (used only when TriggerType = Passive)")]
        [Tooltip("Seconds a target stays primed after the priming apex hits it (Frozen Lightning: 2s).")]
        [SerializeField, Min(0f)] private float _primeWindowSeconds = 2f;
        [Tooltip("Damage multiplier the consuming apex's hit deals against a primed target, applied ON TOP of " +
                 "that apex's own normal damage calc (Frozen Lightning: 2.5 — intentionally large; the strongest " +
                 "upgrade in the game by design, do not soften without explicit instruction).")]
        [SerializeField, Min(1f)] private float _consumeDamageMultiplier = 2.5f;

        public string ComboName => _comboName;

        /// <summary>Task 43: Codex flavour/effect text (may be empty for older assets).</summary>
        public string Description => _description;
        public Sprite Icon => _icon;

        /// <summary>Passive vs. Active (Task 38). Only Passive is implemented this task.</summary>
        public ComboApexTriggerType TriggerType => _triggerType;

        /// <summary>The apex whose hit primes a target (Passive combos).</summary>
        public ApexTalentDefinitionSO PrimingApex => _primingApex;

        /// <summary>The apex that consumes a primed target for amplified damage (Passive combos).</summary>
        public ApexTalentDefinitionSO ConsumingApex => _consumingApex;

        /// <summary>Seconds a target stays primed after the priming hit.</summary>
        public float PrimeWindowSeconds => _primeWindowSeconds;

        /// <summary>Damage multiplier applied to the consuming hit against a primed target.</summary>
        public float ConsumeDamageMultiplier => _consumeDamageMultiplier;
    }
}
