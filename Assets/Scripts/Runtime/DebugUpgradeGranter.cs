using UnityEngine;
using UnityEngine.InputSystem;
using Wavekeep.Core;
using Wavekeep.Data;

namespace Wavekeep.Runtime
{
    /// <summary>
    /// Debug-only testing scaffolding. The real level-up flow is now Task 07's
    /// <c>LevelUpCardPicker</c>; this just grants test <see cref="UpgradeDefinitionSO"/>s to the
    /// session's <c>UpgradeInventory</c> via number keys 1..N for quick manual testing of
    /// tag-interaction effects. Gated behind <see cref="_enableDebugKeys"/> (Task 07 §4) so normal
    /// play never depends on it. Kept separate from <see cref="HeroRuntime"/> so the real hero driver
    /// isn't polluted with debug input.
    /// </summary>
    [AddComponentMenu("Wavekeep/Debug/Debug Upgrade Granter")]
    public sealed class DebugUpgradeGranter : MonoBehaviour
    {
        [Tooltip("Toggle the 1/2/3 test-grant keys. Off by default — the card picker drives real play.")]
        [SerializeField] private bool _enableDebugKeys;
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [Tooltip("Granted by number keys 1..N (a testing shortcut alongside the Task 07 card picker).")]
        [SerializeField] private UpgradeDefinitionSO[] _debugUpgrades;

        private static readonly Key[] GrantKeys = { Key.Digit1, Key.Digit2, Key.Digit3 };

        private void Update()
        {
            if (!_enableDebugKeys) return;
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
