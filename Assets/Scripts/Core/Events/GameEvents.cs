using UnityEngine;
using Wavekeep.Data;
using Wavekeep.Gear;

namespace Wavekeep.Core.Events
{
    // Event payloads for the signals listed in CLAUDE.md §3.3.
    // These are grouped in one file as they are tiny related marker types; behaviour
    // lives elsewhere, so this does not violate the "one class per file" convention
    // for actual systems.

    /// <summary>Published when an enemy dies (health reaches zero). Carries the dead enemy's
    /// <see cref="EnemyDefinitionSO"/> so reward consumers (CurrencyManager/XPManager) can read
    /// its currency/xp yields, plus the <see cref="LootTable"/> resolved for THIS kill (Task 13) —
    /// the enemy's own table for regulars, or the wave's boss table for bosses (null = no drops).
    /// The SOs are read-only — consumers must never write to them (CLAUDE.md §3.5).</summary>
    public readonly struct EnemyKilledEvent
    {
        public readonly EnemyDefinitionSO Definition;
        public readonly LootTableSO LootTable;

        /// <summary>Task 69: world-space position of the enemy at the moment of death. Purely presentational
        /// metadata (it does not affect currency/xp/loot logic) — it lets the visual loot-drop layer place a
        /// marker where the enemy fell, and is forwarded into <see cref="GearDroppedEvent.DropPosition"/>.</summary>
        public readonly Vector3 DeathPosition;

        public EnemyKilledEvent(EnemyDefinitionSO definition, LootTableSO lootTable, Vector3 deathPosition)
        {
            Definition = definition;
            LootTable = lootTable;
            DeathPosition = deathPosition;
        }
    }

    /// <summary>Published when a kill rolls an actual gear/artifact drop (Task 13; Task 68 carries a generated
    /// instance). Carries the freshly generated <see cref="GearInstance"/> for the minimal in-run pickup
    /// notification (it is already in the GearInventory by the time this fires).</summary>
    public readonly struct GearDroppedEvent
    {
        public readonly GearInstance Item;

        /// <summary>Task 69: world-space position to show the visual drop marker at (the enemy's death position,
        /// forwarded from <see cref="EnemyKilledEvent.DeathPosition"/>). Presentational only.</summary>
        public readonly Vector3 DropPosition;

        public GearDroppedEvent(GearInstance item, Vector3 dropPosition)
        {
            Item = item;
            DropPosition = dropPosition;
        }
    }

    // (Removed) EnemyReachedDefendedPointEvent — superseded by attack-the-wall behavior.
    // Enemies no longer resolve on arrival; they stop and attack WallRuntime on an interval and
    // are only released to the pool on death. Wall destruction ends the run via RunEndedEvent.

    /// <summary>Published when a wave begins.</summary>
    public readonly struct WaveStartedEvent
    {
        public readonly int WaveIndex;
        public WaveStartedEvent(int waveIndex) => WaveIndex = waveIndex;
    }

    /// <summary>Published when a wave is fully cleared.</summary>
    public readonly struct WaveCompletedEvent
    {
        public readonly int WaveIndex;
        public WaveCompletedEvent(int waveIndex) => WaveIndex = waveIndex;
    }

    /// <summary>Published when the spawner pauses between two waves and is waiting for the player to
    /// continue (Task 06). A GENERAL pause hook — the spawner does not know about the shop; the shop
    /// UI subscribes to open, and releases the gate via <c>WaveSpawner.ContinueAfterIntermission</c>.
    /// Only fires when a next wave exists (never after the final wave).</summary>
    public readonly struct IntermissionStartedEvent
    {
        public readonly int CompletedWaveIndex;
        public readonly int NextWaveIndex;
        public IntermissionStartedEvent(int completedWaveIndex, int nextWaveIndex)
        {
            CompletedWaveIndex = completedWaveIndex;
            NextWaveIndex = nextWaveIndex;
        }
    }

    /// <summary>Published when the player picks a hero and the run begins (Task 11). Carries the chosen
    /// <see cref="HeroDefinitionSO"/> so systems like the level-up card picker can read that hero's
    /// exclusive upgrade pool. The SO is read-only.</summary>
    public readonly struct HeroSelectedEvent
    {
        public readonly HeroDefinitionSO Hero;
        public HeroSelectedEvent(HeroDefinitionSO hero) => Hero = hero;
    }

    /// <summary>Task 43: published by <c>HeroRuntime</c> the moment a single-hero apex talent first unlocks
    /// during a run (its required lines all hit max tier). Carries the unlocked
    /// <see cref="ApexTalentDefinitionSO"/> (read-only). The discovery manager listens to record first-ever
    /// discoveries and to re-evaluate cross-hero combo unlocks. NOT a per-frame signal — fires once per apex
    /// per run.</summary>
    public readonly struct ApexUnlockedEvent
    {
        public readonly ApexTalentDefinitionSO Apex;
        public ApexUnlockedEvent(ApexTalentDefinitionSO apex) => Apex = apex;
    }

    /// <summary>Task 43: published when an apex or combo apex is discovered for the FIRST time ever (it was
    /// not already in the persistent discovered set). Drives the distinct first-discovery notification — it is
    /// NOT republished on later unlocks of an already-discovered talent. Carries the talent's display name and
    /// whether it is a combo apex, so the banner can word itself ("New Combo Discovered!" vs apex).</summary>
    public readonly struct TalentDiscoveredEvent
    {
        public readonly string TalentName;
        public readonly bool IsCombo;
        public TalentDiscoveredEvent(string talentName, bool isCombo)
        {
            TalentName = talentName;
            IsCombo = isCombo;
        }
    }

    /// <summary>Published when the active hero gains an XP level.</summary>
    public readonly struct XPLevelUpEvent
    {
        public readonly int NewLevel;
        public XPLevelUpEvent(int newLevel) => NewLevel = newLevel;
    }

    /// <summary>Published when the player's currency total changes.</summary>
    public readonly struct CurrencyChangedEvent
    {
        public readonly int NewTotal;
        public CurrencyChangedEvent(int newTotal) => NewTotal = newTotal;
    }

    /// <summary>Published when the run's reroll-point pool changes (Task 09): consumed by a reroll
    /// (−1) or replenished by a Reroll Potion (+tier). Separate from currency — the shop UI subscribes
    /// to refresh the reroll count/button independently of <see cref="CurrencyChangedEvent"/>.</summary>
    public readonly struct RerollPointsChangedEvent
    {
        public readonly int NewTotal;
        public RerollPointsChangedEvent(int newTotal) => NewTotal = newTotal;
    }

    /// <summary>Published when a run ends. Carries a minimal <see cref="RunResult"/>
    /// (see <c>RunResult.cs</c>). Task 02 fires this with <see cref="RunOutcome.WavesCleared"/>
    /// after the final wave resolves; defeat/game-over paths come in a later task.</summary>
    public readonly struct RunEndedEvent
    {
        public readonly RunResult Result;
        public RunEndedEvent(RunResult result) => Result = result;
    }
}
