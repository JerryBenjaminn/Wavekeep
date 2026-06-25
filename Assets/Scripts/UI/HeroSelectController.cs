using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Core.Events;
using Wavekeep.Data;
using Wavekeep.Runtime;
using Wavekeep.Waves;

namespace Wavekeep.UI
{
    /// <summary>
    /// Pre-run hero-select screen. Reads a serialized roster of <see cref="HeroDefinitionSO"/> and
    /// builds one button per hero AT RUNTIME (a loop, no hero-specific code) — so adding a third hero
    /// is purely adding a third asset to the roster, with zero code changes (Task 05 §5).
    ///
    /// On selection it hides the panel, instantiates the chosen hero's prefab at the spawn point,
    /// initialises its <see cref="HeroRuntime"/>, and starts the Task 02 wave run (which is configured
    /// not to auto-start, so the run waits for the player's choice).
    ///
    /// Task 14: the Hub now owns hero selection. If a <see cref="RunLaunchContext"/> carries a hero
    /// (the normal launch path), this panel is skipped and that hero auto-starts — so the player never
    /// picks twice. The in-scene panel survives only as a DEV fallback for opening the gameplay scene
    /// standalone (no Hub), keeping Task 05 testable in isolation.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/Hero Select Controller")]
    public sealed class HeroSelectController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private GameSessionBootstrap _bootstrap;
        [SerializeField] private WaveSpawner _waveSpawner;
        [SerializeField] private Transform _heroSpawnPoint;

        [Header("Roster (add a HeroDefinitionSO here to add a hero — no code needed)")]
        [SerializeField] private List<HeroDefinitionSO> _heroRoster = new List<HeroDefinitionSO>();

        [Header("Debug Team (Task 36 — temporary dual-hero entry point)")]
        [Tooltip("If non-empty, the run starts immediately with ALL these heroes active together (skipping " +
                 "single-hero select / launch context). The proper Hub team-select UI is a later task; this " +
                 "is the 'hardcoded/debug entry point' Task 36 assumes (Frost Warden + Bolt Striker).")]
        [SerializeField] private List<HeroDefinitionSO> _debugTeam = new List<HeroDefinitionSO>();
        [Tooltip("Lateral spacing (m) between spawned team members so they don't overlap at the near edge.")]
        [SerializeField, Min(0f)] private float _teamSpacing = 3f;

        [Header("UI")]
        [Tooltip("Root object hidden once a hero is chosen.")]
        [SerializeField] private GameObject _selectPanel;
        [Tooltip("Parent (with a layout group) under which hero buttons are generated.")]
        [SerializeField] private RectTransform _buttonContainer;

        private bool _chosen;

        private void Start()
        {
            // Task 36: dual-hero debug entry point takes priority — start the whole hardcoded team at once.
            if (_debugTeam != null && _debugTeam.Count > 0)
            {
                if (_selectPanel != null) _selectPanel.SetActive(false);
                StartCoroutine(AutoStartTeamNextFrame());
                return;
            }

            // Task 14: Hub-launched run — auto-start the hero chosen in the Hub, skipping the panel.
            var launch = Object.FindFirstObjectByType<RunLaunchContext>();
            if (launch != null && launch.SelectedHero != null)
            {
                if (_selectPanel != null) _selectPanel.SetActive(false);
                StartCoroutine(AutoStartNextFrame(launch.SelectedHero));
                return;
            }

            // Standalone (no Hub): show the dev-fallback hero-select panel.
            BuildButtons();
        }

        // Defer one frame so every component's Start() (notably WaveSpawner's) has run before we
        // instantiate the hero and call StartRun — avoids Start-ordering NREs that the original
        // button-click flow never hit (the click always came after all Starts).
        private IEnumerator AutoStartNextFrame(HeroDefinitionSO hero)
        {
            yield return null;
            OnHeroChosen(hero);
        }

        // Task 36: same one-frame defer, but starts the whole team together.
        private IEnumerator AutoStartTeamNextFrame()
        {
            yield return null;
            StartTeam(_debugTeam);
        }

        private void BuildButtons()
        {
            if (_buttonContainer == null) return;

            for (int i = 0; i < _heroRoster.Count; i++)
            {
                var hero = _heroRoster[i];
                if (hero == null) continue;

                CreateHeroButton(hero);
            }
        }

        private void CreateHeroButton(HeroDefinitionSO hero)
        {
            var go = new GameObject($"Button_{hero.HeroName}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_buttonContainer, false);

            var image = go.GetComponent<Image>();
            image.color = hero.Tint; // placeholder: button tinted to match the hero capsule

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(280f, 56f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = hero.HeroName;
            label.fontSize = 26f;
            label.color = Color.black;
            label.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            // Capture the hero in the closure so the handler is data-driven, not hero-specific.
            go.GetComponent<Button>().onClick.AddListener(() => OnHeroChosen(hero));
        }

        // Single-hero path (Hub launch / dev panel) — delegates to the team path with a one-hero team so
        // spawn/registration/run-start logic lives in exactly one place (Task 36).
        private void OnHeroChosen(HeroDefinitionSO hero)
        {
            StartTeam(new List<HeroDefinitionSO> { hero });
        }

        // Task 36: spawn every hero in the team side-by-side at the near edge, initialise each (they
        // self-register into the session's HeroRegistry), then start the run once.
        private void StartTeam(IReadOnlyList<HeroDefinitionSO> team)
        {
            if (_chosen) return;
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogError("[HeroSelectController] No GameSessionBootstrap/Session; cannot start.", this);
                return;
            }
            if (team == null || team.Count == 0)
            {
                Debug.LogError("[HeroSelectController] Empty team; cannot start.", this);
                return;
            }

            _chosen = true;
            if (_selectPanel != null) _selectPanel.SetActive(false);

            var basePos = _heroSpawnPoint != null ? _heroSpawnPoint.position : Vector3.zero;
            var rot = _heroSpawnPoint != null ? _heroSpawnPoint.rotation : Quaternion.identity;
            // Lateral axis to spread members along — the spawn point's right (falls back to world X). The
            // arena is open width-wise (CLAUDE.md §2), so spreading on X keeps everyone at the near edge.
            var right = _heroSpawnPoint != null ? _heroSpawnPoint.right : Vector3.right;

            int spawned = 0;
            for (int i = 0; i < team.Count; i++)
            {
                var hero = team[i];
                if (hero == null) continue;
                // Centre the row: offsets are symmetric around the spawn point.
                float offset = (i - (team.Count - 1) * 0.5f) * _teamSpacing;
                SpawnHero(hero, basePos + right * offset, rot);
                spawned++;
            }

            if (spawned == 0)
            {
                Debug.LogError("[HeroSelectController] No valid heroes in team (all null/prefabless).", this);
                return;
            }

            Debug.Log($"[HeroSelectController] Started run with {spawned} hero(es).");
            if (_waveSpawner != null) _waveSpawner.StartRun();
        }

        private void SpawnHero(HeroDefinitionSO hero, Vector3 position, Quaternion rotation)
        {
            if (hero.Prefab == null)
            {
                Debug.LogError($"[HeroSelectController] Hero '{hero.HeroName}' has no prefab assigned.", this);
                return;
            }

            var instance = Object.Instantiate(hero.Prefab, position, rotation);
            instance.name = $"Hero ({hero.HeroName})";

            var heroRuntime = instance.GetComponent<HeroRuntime>();
            if (heroRuntime == null) heroRuntime = instance.AddComponent<HeroRuntime>();
            heroRuntime.Initialize(hero, _bootstrap.Session, _waveSpawner); // self-registers into Session.Heroes

            // Task 11/14: announce each spawned hero (gear-debug + any per-hero listeners). With a team, this
            // fires once per hero; listeners that track a single "active hero" simply see the last one.
            _bootstrap.Session.Events.Publish(new HeroSelectedEvent(hero));

            Debug.Log($"[HeroSelectController] Spawned hero '{hero.HeroName}' at {position}.");
        }
    }
}
