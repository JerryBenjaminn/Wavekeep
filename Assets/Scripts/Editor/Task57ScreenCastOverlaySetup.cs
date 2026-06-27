#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Wavekeep.Core;
using Wavekeep.UI;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 57 (Part B) — adds the generic screen cast-overlay system to the gameplay scene: a dedicated
    /// high-sort-order overlay Canvas hosting a <see cref="ScreenCastOverlayController"/>, wired into the
    /// scene's <see cref="GameSessionBootstrap"/> so heroes can flash their Ultimate-cast overlay through the
    /// session (no static singleton, §3.5). Idempotent — safe to re-run. Run it in the gameplay scene, then save.
    /// </summary>
    public static class Task57ScreenCastOverlaySetup
    {
        private const string CanvasName = "ScreenCastOverlay Canvas";
        private const string ControllerName = "ScreenCastOverlay";
        private const int OverlaySortingOrder = 200; // above the gameplay HUD canvases

        [MenuItem("Wavekeep/Setup Task 57 (Screen Cast Overlay)")]
        public static void SetupScene()
        {
            var bootstrap = Object.FindFirstObjectByType<GameSessionBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                Debug.LogError("[Task57] No GameSessionBootstrap in the scene. Open the gameplay scene and re-run.");
                return;
            }

            var controller = EnsureOverlayController();

            // Wire the controller into the bootstrap's serialized field (idempotent).
            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("_screenCastOverlay");
            if (prop == null)
            {
                Debug.LogError("[Task57] GameSessionBootstrap has no '_screenCastOverlay' field — script/asset out of sync.");
                return;
            }
            prop.objectReferenceValue = controller;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Task57] Screen cast-overlay system wired (overlay Canvas + ScreenCastOverlayController → " +
                      "GameSessionBootstrap). Frost Warden's Ultimate now flashes its overlay. Save the scene (Ctrl+S).");
        }

        private static ScreenCastOverlayController EnsureOverlayController()
        {
            var existing = Object.FindFirstObjectByType<ScreenCastOverlayController>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            // Dedicated overlay Canvas, drawn above the gameplay HUD. CanvasScaler matches the project convention
            // (§3.6): scale with screen size, 1920×1080 reference, match width-or-height.
            var canvasGo = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // The raycaster on the overlay canvas would otherwise sit above gameplay UI; the controller's images
            // set raycastTarget=false, but disable the raycaster too so this layer never intercepts input.
            canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

            // Full-screen host RectTransform carrying the controller; its pooled images stretch to fill it.
            var hostGo = new GameObject(ControllerName, typeof(RectTransform), typeof(ScreenCastOverlayController));
            var rect = hostGo.GetComponent<RectTransform>();
            rect.SetParent(canvasGo.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return hostGo.GetComponent<ScreenCastOverlayController>();
        }
    }
}
#endif
