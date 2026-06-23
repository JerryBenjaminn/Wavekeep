#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// One-shot editor utility that adds the Task 21 ultimate charge bar to the existing Canvas: a
    /// bottom-centre background, a horizontally-filled bar driven by the ultimate's cooldown progress,
    /// and a label that flips to a "ready" state. Wires a <see cref="UltimateChargeBar"/> to those
    /// elements; the bar finds the runtime-spawned hero itself.
    ///
    /// Built in code for the same reason as the other task setups (scenes aren't hand-authored as YAML).
    /// Run "Wavekeep/Setup Task 21 (Ultimate Charge Bar)" after the earlier setups, then save the scene.
    /// Editor-only; not part of the runtime build.
    /// </summary>
    public static class Task21SceneSetup
    {
        private const string RootName = "UltimateChargeBar";

        [MenuItem("Wavekeep/Setup Task 21 (Ultimate Charge Bar)")]
        public static void SetupScene()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Task21SceneSetup] No Canvas in scene. Run 'Wavekeep/Setup Task 01 Scene' first.");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning("[Task21SceneSetup] TMP has no default font asset. If the label doesn't render, " +
                                 "import it via Window > TextMeshPro > Import TMP Essential Resources, then re-run.");
            }

            DestroyIfExists(RootName); // idempotent re-run

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Root: anchored bottom-centre, acts as the dark background of the bar.
            var rootGo = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootGo.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(0.5f, 0f);
            rootRt.anchorMax = new Vector2(0.5f, 0f);
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.sizeDelta = new Vector2(420f, 36f);
            rootRt.anchoredPosition = new Vector2(0f, 36f);
            var background = rootGo.GetComponent<Image>();
            background.sprite = uiSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0f, 0f, 0f, 0.65f);

            // Fill: a horizontally-filled image inset slightly inside the background.
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(rootGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = uiSprite;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.color = new Color(0.20f, 0.40f, 0.90f, 1f);

            // Label: centred over the bar.
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rootGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Ultimate 0%";
            label.fontSize = 20f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var bar = rootGo.AddComponent<UltimateChargeBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("_fillImage").objectReferenceValue = fillImage;
            so.FindProperty("_label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task21SceneSetup] Ultimate charge bar added. Play: the bar fills over the ultimate " +
                      "cooldown and shows 'READY'; pressing U mid-cooldown does nothing. Save the scene (Ctrl+S).");
        }

        private static void DestroyIfExists(string objectName)
        {
            var existing = GameObject.Find(objectName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }
    }
}
#endif
