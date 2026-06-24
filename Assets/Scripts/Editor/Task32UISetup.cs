#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 32 (UI-only) — two scene tweaks in the gameplay scene:
    /// <list type="number">
    /// <item>Widens the level-up card row so the larger Task 32 cards (built at runtime by
    ///   <see cref="LevelUpCardPicker"/>, now with a skill badge + bigger text) lay out centred without
    ///   clipping. Card size/badge themselves are runtime — only the container width needs adjusting.</item>
    /// <item>Adds an <see cref="ApexCooldownDisplay"/> just above the ultimate charge bar: a vertical
    ///   stack of per-apex cooldown bars (created at runtime once apexes unlock), reusing the built-in
    ///   UISprite so they match the ultimate bar's fill/empty look.</item>
    /// </list>
    /// Run "Wavekeep/Setup Task 32 (UI Clarity)" in the gameplay scene after the Task 07/21 setups, then
    /// save the scene. Idempotent. Editor-only.
    /// </summary>
    public static class Task32UISetup
    {
        private const string ApexRootName = "ApexCooldownBars";

        [MenuItem("Wavekeep/Setup Task 32 (UI Clarity)")]
        public static void SetupScene()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Task32] No Canvas in scene. Run the Task 01/07/21 setups first.");
                return;
            }

            WidenCardRow();
            BuildApexDisplay(canvas);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task32] UI clarity pass applied: level-up card row widened for the larger badged cards; " +
                      "apex cooldown bars added above the ultimate bar (appear once an apex unlocks). Save the scene (Ctrl+S).");
        }

        // The cards are built at runtime; here we just give their row enough width to centre the larger cards.
        private static void WidenCardRow()
        {
            var picker = Object.FindFirstObjectByType<LevelUpCardPicker>();
            if (picker == null)
            {
                Debug.LogWarning("[Task32] No LevelUpCardPicker found; skipped widening the card row. Run the Task 07 setup.");
                return;
            }

            var so = new SerializedObject(picker);
            var container = so.FindProperty("_cardContainer").objectReferenceValue as RectTransform;
            if (container != null)
            {
                // 3 cards × 340 + spacing/padding → ~1120 wide, tall enough for the 440 cards.
                container.sizeDelta = new Vector2(1120f, 470f);
            }
            else
            {
                Debug.LogWarning("[Task32] LevelUpCardPicker._cardContainer not wired; couldn't widen the card row.");
            }
        }

        private static void BuildApexDisplay(Canvas canvas)
        {
            DestroyIfExists(ApexRootName); // idempotent re-run

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Container: bottom-centre, just above the ultimate charge bar (which sits at y≈36, h≈36).
            var rootGo = new GameObject(ApexRootName,
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            rootGo.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)rootGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 80f); // above the 36px-tall ultimate bar at y=36
            rt.sizeDelta = new Vector2(380f, 0f);

            var vlg = rootGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            rootGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var display = rootGo.AddComponent<ApexCooldownDisplay>();
            var so = new SerializedObject(display);
            so.FindProperty("_container").objectReferenceValue = rt;
            so.FindProperty("_barSprite").objectReferenceValue = uiSprite;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }
}
#endif
