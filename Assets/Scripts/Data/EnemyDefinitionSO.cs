using UnityEngine;

namespace Wavekeep.Data
{
    /// <summary>
    /// Designer-authored enemy template (CLAUDE.md §3.1). Read-only at runtime — all live state
    /// (current health, working stats after multipliers) lives in <c>EnemyRuntime</c>, never
    /// written back here (§3.5). The prefab is pooled, not Instantiated per spawn (§3.5).
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Wavekeep/Enemy Definition")]
    public sealed class EnemyDefinitionSO : ScriptableObject
    {
        [SerializeField] private string _enemyName;

        [Header("Prefab")]
        [Tooltip("Pooled 3D prefab (Collider/Rigidbody as appropriate). Spawned via EnemyPoolManager.")]
        [SerializeField] private GameObject _prefab;

        [Header("Base Stats")]
        [SerializeField] private float _maxHealth = 10f;
        [SerializeField] private float _moveSpeed = 3f;
        [Tooltip("Damage dealt to the defended point on arrival. Stored now; applied in a later task.")]
        [SerializeField] private float _contactDamage = 5f;

        [Header("Loot Yield (consumed by Task 03 — stored here only)")]
        [SerializeField] private int _currencyReward = 1;
        [SerializeField] private int _xpReward = 1;

        [Header("Gear Drops (Task 13)")]
        [Tooltip("Optional loot table rolled on death (regular enemies). Null = drops nothing. " +
                 "Bosses ignore this — their table comes from the wave's boss entry (WaveConfigSO).")]
        [SerializeField] private LootTableSO _lootTable;

        public string EnemyName => _enemyName;
        public GameObject Prefab => _prefab;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float ContactDamage => _contactDamage;
        public int CurrencyReward => _currencyReward;
        public int XpReward => _xpReward;
        public LootTableSO LootTable => _lootTable;
    }
}
