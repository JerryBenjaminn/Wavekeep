using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.Core.Events;

namespace Wavekeep.UI
{
    /// <summary>
    /// Task 43: in-game banner shown the FIRST time ever a player unlocks an apex or combo apex (driven by
    /// <see cref="TalentDiscoveredEvent"/>, which the discovery manager publishes only on a genuinely new
    /// discovery). Deliberately DISTINCT from the Task 32 apex cooldown bars / Task 38 combo "ACTIVE" badge:
    /// those communicate "this is live THIS run"; this one communicates "you have permanently LEARNED this for
    /// future runs." Subsequent unlocks of an already-discovered talent never reach here (no event fires).
    ///
    /// Self-building placeholder UI (a centred banner) created under a wired container — the editor setup only
    /// supplies the container + a sprite + the session source. Reads the EventBus from the session via the
    /// bootstrap (no static singleton, CLAUDE.md §3.5). Banners queue and show one at a time so two near-
    /// simultaneous discoveries (e.g. an apex that immediately completes a combo) are both readable. The
    /// countdown uses real <c>deltaTime</c> (the run's PauseState is a flag, not a timescale change), so the
    /// banner still ticks down even though discoveries happen during the paused level-up screen.
    /// </summary>
    [AddComponentMenu("Wavekeep/UI/First Discovery Notification Display")]
    public sealed class FirstDiscoveryNotificationDisplay : MonoBehaviour
    {
        [Tooltip("Parent the banner is built under (e.g. a top-centre anchored RectTransform).")]
        [SerializeField] private RectTransform _container;
        [Tooltip("Sprite for the banner background (wire the built-in UI/Skin/UISprite).")]
        [SerializeField] private Sprite _bannerSprite;
        [Tooltip("Session source for the EventBus. Required — discovery is a run service, not a scene object.")]
        [SerializeField] private GameSessionBootstrap _bootstrap;

        [Header("Behaviour")]
        [Tooltip("Seconds each discovery banner stays on screen before the next queued one shows.")]
        [SerializeField, Min(0.5f)] private float _displaySeconds = 4f;
        [SerializeField] private Color _bannerColor = new Color(0.45f, 0.30f, 0.70f, 0.95f);

        private EventBus _events;
        private GameObject _bannerRoot;
        private TMP_Text _label;
        private readonly Queue<string> _pending = new Queue<string>();
        private float _remaining;

        private void Start()
        {
            var session = _bootstrap != null ? _bootstrap.Session : null;
            if (session == null)
            {
                Debug.LogError("[FirstDiscoveryNotificationDisplay] No GameSessionBootstrap/Session; disabling.", this);
                enabled = false;
                return;
            }

            _events = session.Events;
            _events.Subscribe<TalentDiscoveredEvent>(OnTalentDiscovered);

            BuildBanner();
            HideBanner();
        }

        private void OnDestroy()
        {
            if (_events != null) _events.Unsubscribe<TalentDiscoveredEvent>(OnTalentDiscovered);
        }

        private void OnTalentDiscovered(TalentDiscoveredEvent evt)
        {
            string kind = evt.IsCombo ? "NEW COMBO DISCOVERED!" : "NEW APEX DISCOVERED!";
            string name = string.IsNullOrEmpty(evt.TalentName) ? "Unknown Talent" : evt.TalentName;
            // Two lines: the "you learned something permanent" headline + the talent name.
            _pending.Enqueue($"<size=70%><color=#E6D8FF>{kind}</color></size>\n<b>{name}</b>");
            if (_bannerRoot != null && !_bannerRoot.activeSelf) ShowNext();
        }

        private void Update()
        {
            if (_bannerRoot == null || !_bannerRoot.activeSelf) return;

            _remaining -= Time.deltaTime;
            if (_remaining > 0f) return;

            if (_pending.Count > 0) ShowNext();
            else HideBanner();
        }

        private void ShowNext()
        {
            if (_pending.Count == 0) { HideBanner(); return; }
            if (_label != null) _label.text = _pending.Dequeue();
            if (_bannerRoot != null) _bannerRoot.SetActive(true);
            _remaining = _displaySeconds;
        }

        private void HideBanner()
        {
            if (_bannerRoot != null) _bannerRoot.SetActive(false);
        }

        private void BuildBanner()
        {
            if (_container == null) return;

            _bannerRoot = new GameObject("DiscoveryBanner", typeof(RectTransform), typeof(Image));
            _bannerRoot.transform.SetParent(_container, false);
            var rt = (RectTransform)_bannerRoot.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(560f, 90f);
            var bg = _bannerRoot.GetComponent<Image>();
            bg.sprite = _bannerSprite;
            bg.type = Image.Type.Sliced;
            bg.color = _bannerColor;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_bannerRoot.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 30f;
            _label.color = Color.white;
            _label.alignment = TextAlignmentOptions.Center;
            _label.richText = true;
            if (TMP_Settings.defaultFontAsset != null) _label.font = TMP_Settings.defaultFontAsset;
            var lrt = _label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 8f);
            lrt.offsetMax = new Vector2(-12f, -8f);
        }
    }
}
