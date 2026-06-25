using System.Collections.Generic;
using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Task 43: master registry of every <see cref="ApexTalentDefinitionSO"/> and
    /// <see cref="ComboApexTalentDefinitionSO"/> in the project, mirroring the Task 12
    /// <see cref="GearCatalogSO"/> pattern. The Hub Codex iterates this to list all talents (showing full
    /// detail for discovered ones, "???" for the rest) WITHOUT hardcoding entries — the editor setup
    /// (<c>Task43CodexSetup</c>) re-scans the project to fill it, so newly authored apex/combo assets appear
    /// automatically. A runtime build can't enumerate the AssetDatabase, so this authored asset is how the
    /// "all existing talents" list reaches the runtime Codex. Read-only at runtime (CLAUDE.md §3.5).
    /// </summary>
    [CreateAssetMenu(fileName = "TalentCatalog", menuName = "Wavekeep/Talent Catalog")]
    public sealed class TalentCatalogSO : ScriptableObject
    {
        [SerializeField] private List<ApexTalentDefinitionSO> _apexTalents = new List<ApexTalentDefinitionSO>();
        [SerializeField] private List<ComboApexTalentDefinitionSO> _comboApexes = new List<ComboApexTalentDefinitionSO>();

        /// <summary>Every single-hero apex talent that exists in the project.</summary>
        public IReadOnlyList<ApexTalentDefinitionSO> ApexTalents => _apexTalents;

        /// <summary>Every cross-hero combo apex talent that exists in the project.</summary>
        public IReadOnlyList<ComboApexTalentDefinitionSO> ComboApexes => _comboApexes;
    }
}
