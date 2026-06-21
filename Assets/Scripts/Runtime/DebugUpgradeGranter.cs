using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Core;
using Wavekeep.Data;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// THROWAWAY debug scaffolding (replaced by Task 07's level-up card picker). Grants test
    /// <see cref="UpgradeDefinitionSO"/>s to the session's <c>UpgradeInventory</c> via number keys
    /// 1..N, so tag-interaction effects on hero abilities remain testable in Task 05. Kept separate
    /// from <see cref="HeroRuntime"/> so the real hero driver isn't polluted with debug input.
    /// </summary>
    [AddComponentMenu("Wavekeep/Debug/Debug Upgrade Granter")]
    public sealed class DebugUpgradeGranter : MonoBehaviour
    {
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [Tooltip("Granted by number keys 1..N (stands in for the Task 07 card picker).")]
        [SerializeField] private UpgradeDefinitionSO[] _debugUpgrades;

        private static readonly Key[] GrantKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

        private void Update()
        {
            if (_bootstrap == null || _bootstrap.Session == null || _debugUpgrades == null) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            int count = Mathf.Min(_debugUpgrades.Length, GrantKeys.Length);
            for (int i = 0; i < count; i++)
            {
                if (!keyboard[GrantKeys[i]].wasPressedThisFrame) continue;

                var upgrade = _debugUpgrades[i];
                if (upgrade == null) continue;

                _bootstrap.Session.UpgradeInventory.Add(upgrade);
                Debug.Log($"[DebugUpgradeGranter] Granted '{upgrade.UpgradeName}'.");
            }
        }
    }
}
