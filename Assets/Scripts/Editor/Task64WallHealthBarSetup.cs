#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Runtime;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Adds the wall health bar to the gameplay Canvas: a top-centre background, a horizontally-filled bar
    /// driven by <see cref="WallRuntime"/> CurrentHP/MaxHP, and a numeric "cur / max" label. Wires a
    /// <see cref="WallHealthBar"/> to those elements + the scene's wall. Built in code for the same reason
    /// as the other task setups (scenes aren't hand-authored as YAML — see the project memory).
    ///
    /// Run "Wavekeep/Setup Task 64 (Wall Health Bar)" from the gameplay scene (SampleScene), then save it.
    /// Idempotent (re-running replaces the existing bar). Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task64WallHealthBarSetup
    {
        private const string RootName = "WallHealthBar";

        [MenuItem("Wavekeep/Setup Task 64 (Wall Health Bar)")]
        public static void SetupScene()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Task64] No Canvas in scene. Run 'Wavekeep/Setup Task 01 Scene' first.");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task64] TMP has no default font asset. If the label doesn't render, import it " +
                                 "via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            DestroyIfExists(RootName); // idempotent re-run

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Root = top-centre background panel.
            var rootGo = new GameObject(RootName, typeof(RectTransform), typeof(Image));
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.sizeDelta = new Vector2(440f, 30f);
            rootRt.anchoredPosition = new Vector2(0f, -12f);
            var bg = rootGo.GetComponent<Image>();
            bg.sprite = uiSprite;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0.65f);

            // Horizontally-filled health fill (inset from the background edges).
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(rootGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            var fill = fillGo.GetComponent<Image>();
            fill.sprite = uiSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.color = new Color(0.30f, 0.80f, 0.30f, 1f);

            // Numeric label, drawn over the fill (added after it so it renders on top).
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rootGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.text = "Wall";
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            var lRt = label.rectTransform;
            lRt.anchorMin = Vector2.zero;
            lRt.anchorMax = Vector2.one;
            lRt.offsetMin = Vector2.zero;
            lRt.offsetMax = Vector2.zero;

            var bar = rootGo.AddComponent<WallHealthBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("_fill").objectReferenceValue = fill;
            so.FindProperty("_label").objectReferenceValue = label;
            var wall = Object.FindFirstObjectByType<WallRuntime>();
            if (wall != null) so.FindProperty("_wall").objectReferenceValue = wall;
            else Debug.LogWarning("[Task64] No WallRuntime in scene — the bar will scene-scan for it at runtime.");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task64] Wall health bar added (top-centre, fill lerps green→red as HP drops). " +
                      "Save the scene (Ctrl+S).");
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing);
        }
    }
}
#endif
