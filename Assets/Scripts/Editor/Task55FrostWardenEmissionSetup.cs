#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 55 — wires the permanent pulsing frost emission onto the Frost Warden model. Claude Code can't drive
    /// the Unity Editor, so this is a one-time, idempotent <see cref="MenuItem"/> the developer runs once.
    ///
    /// It creates a material from the hand-written <c>Wavekeep/FrostWardenEmission</c> shader and adds it as a
    /// SECOND material element on every renderer of the Frost Warden prefab (an additive glow pass over the whole
    /// model). The developer's existing base material is never modified — important here because that base
    /// material (PolygonFantasyCharacters_01_A) is SHARED across Synty characters, so editing it would tint
    /// others too. The glow pulses entirely in-shader (no C#); this script only authors/assigns assets.
    /// </summary>
    public static class Task55FrostWardenEmissionSetup
    {
        private const string ShaderName = "Wavekeep/FrostWardenEmission";
        private const string MaterialFolder = "Assets/Materials";
        private const string MaterialPath = "Assets/Materials/FrostWardenEmission.mat";
        private const string FrostWardenPrefabPath =
            "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Male_Rouge_01.prefab";
        // Read-only source for the alpha-cutout match copy — never written to.
        private const string BaseMaterialPath =
            "Assets/Synty/PolygonFantasyCharacters/Materials/PolygonFantasyCharacters_01_A.mat";

        [MenuItem("Wavekeep/Setup Task 55 (Frost Warden Emission)")]
        public static void Run()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Task55] Shader '{ShaderName}' not found. Ensure FrostWardenEmission.shader compiled " +
                               "(check the Console for shader errors), then re-run.");
                return;
            }

            var material = EnsureMaterial(shader);
            int wired = AssignToPrefab(material);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Task55] Frost Warden emission setup complete. Material '{MaterialPath}' added to {wired} " +
                      "renderer slot(s). Tune _FrostEmissionColor / _FrostPulseMin/MaxIntensity / _FrostPulseSpeed " +
                      "on the material to taste (breathing feel is a by-eye call).");
        }

        private static Material EnsureMaterial(Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(MaterialFolder))
                    AssetDatabase.CreateFolder("Assets", "Materials");

                material = new Material(shader) { name = "FrostWardenEmission" };
                // Icy default; all of these stay tunable on the material asset afterwards.
                material.SetColor("_FrostEmissionColor", new Color(0.25f, 0.6f, 1.0f, 1.0f));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            // Copy the base material's albedo map + cutoff so the glow respects the same alpha cut-outs.
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            if (baseMat != null)
            {
                if (baseMat.HasProperty("_Albedo_Map") && material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", baseMat.GetTexture("_Albedo_Map"));
                if (baseMat.HasProperty("_Cutoff") && material.HasProperty("_Cutoff"))
                    material.SetFloat("_Cutoff", baseMat.GetFloat("_Cutoff"));
            }
            else
            {
                Debug.LogWarning($"[Task55] Base material not found at '{BaseMaterialPath}'; the glow won't match " +
                                 "alpha cut-outs (harmless — full coverage). Assign _BaseMap on the material if needed.");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static int AssignToPrefab(Material overlay)
        {
            var root = PrefabUtility.LoadPrefabContents(FrostWardenPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[Task55] Frost Warden prefab not found at '{FrostWardenPrefabPath}'. " +
                               "Material created but not assigned — drag it on as a 2nd material slot manually.");
                return 0;
            }

            int wired = 0;
            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    // Skip any non-mesh renderers (e.g. trails/particles) — body + weapon meshes only.
                    if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;

                    var mats = new List<Material>(r.sharedMaterials);
                    if (mats.Contains(overlay)) continue; // idempotent: already has the overlay pass

                    // The trick only adds a clean EXTRA pass when the appended element maps past the last
                    // submesh — i.e. when materials already cover every submesh. With a multi-submesh mesh that
                    // has fewer materials, appending would instead reassign a submesh away from its base
                    // material. Synty character parts are single-submesh (one material each), so this is just a
                    // safety net: skip + warn rather than silently break a part's base look.
                    var mesh = GetSharedMesh(r);
                    if (mesh != null && mesh.subMeshCount > mats.Count)
                    {
                        Debug.LogWarning($"[Task55] '{r.name}' has {mesh.subMeshCount} submeshes but {mats.Count} " +
                                         "material(s); skipped to avoid displacing a base material. Add the overlay " +
                                         "material manually as an extra slot on this renderer if it needs the glow.");
                        continue;
                    }

                    mats.Add(overlay); // extra element renders an additive pass over the (single-submesh) mesh
                    r.sharedMaterials = mats.ToArray();
                    wired++;
                }

                if (wired > 0) PrefabUtility.SaveAsPrefabAsset(root, FrostWardenPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return wired;
        }

        private static Mesh GetSharedMesh(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr) return smr.sharedMesh;
            if (r.TryGetComponent<MeshFilter>(out var mf)) return mf.sharedMesh;
            return null;
        }
    }
}
#endif
