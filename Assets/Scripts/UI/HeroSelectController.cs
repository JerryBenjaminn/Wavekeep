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

        [Header("UI")]
        [Tooltip("Root object hidden once a hero is chosen.")]
        [SerializeField] private GameObject _selectPanel;
        [Tooltip("Parent (with a layout group) under which hero buttons are generated.")]
        [SerializeField] private RectTransform _buttonContainer;

        private bool _chosen;

        private void Start()
        {
            BuildButtons();
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

        private void OnHeroChosen(HeroDefinitionSO hero)
        {
            if (_chosen) return;
            if (_bootstrap == null || _bootstrap.Session == null)
            {
                Debug.LogError("[HeroSelectController] No GameSessionBootstrap/Session; cannot start.", this);
                return;
            }
            if (hero.Prefab == null)
            {
                Debug.LogError($"[HeroSelectController] Hero '{hero.HeroName}' has no prefab assigned.", this);
                return;
            }

            _chosen = true;
            if (_selectPanel != null) _selectPanel.SetActive(false);

            var spawnPos = _heroSpawnPoint != null ? _heroSpawnPoint.position : Vector3.zero;
            var spawnRot = _heroSpawnPoint != null ? _heroSpawnPoint.rotation : Quaternion.identity;
            var instance = Object.Instantiate(hero.Prefab, spawnPos, spawnRot);
            instance.name = $"Hero ({hero.HeroName})";

            var heroRuntime = instance.GetComponent<HeroRuntime>();
            if (heroRuntime == null) heroRuntime = instance.AddComponent<HeroRuntime>();
            heroRuntime.Initialize(hero, _bootstrap.Session, _waveSpawner);

            // Task 11: announce the active hero so the level-up picker can include this hero's exclusive
            // upgrade pool (and no other hero's). Published before the run starts, so it's cached well
            // before the first level-up.
            _bootstrap.Session.Events.Publish(new HeroSelectedEvent(hero));

            Debug.Log($"[HeroSelectController] Selected hero '{hero.HeroName}'. Starting run.");
            if (_waveSpawner != null) _waveSpawner.StartRun();
        }
    }
}
