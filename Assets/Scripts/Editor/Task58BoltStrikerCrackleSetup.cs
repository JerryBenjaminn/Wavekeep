#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 58 — wires the permanent electric-crackle emission onto the Bolt Striker model. One-time, idempotent
    /// <see cref="MenuItem"/> (Claude Code can't drive the Editor). Same approach as Task 55's Frost Warden glow:
    /// it builds a material from the hand-written <c>Wavekeep/BoltStrikerCrackle</c> shader and adds it as a
    /// SECOND material element on every renderer of the Bolt Striker prefab (an additive crackle pass over the
    /// whole model), never modifying the base materials — important because those PolygonFantasyCharacters
    /// materials are SHARED across Synty characters. The crackle animates entirely in-shader (no C#).
    /// </summary>
    public static class Task58BoltStrikerCrackleSetup
    {
        private const string ShaderName = "Wavekeep/BoltStrikerCrackle";
        private const string MaterialFolder = "Assets/Materials";
        private const string MaterialPath = "Assets/Materials/BoltStrikerCrackle.mat";
        private const string BoltStrikerPrefabPath =
            "Assets/Synty/PolygonFantasyCharacters/Prefabs/SM_Chr_Male_Sorcerer_01.prefab";
        // Read-only source for the alpha-cutout match copy (the shared atlas both base materials use).
        private const string BaseMaterialPath =
            "Assets/Synty/PolygonFantasyCharacters/Materials/PolygonFantasyCharacters_01_A.mat";

        [MenuItem("Wavekeep/Setup Task 58 (Bolt Striker Crackle)")]
        public static void Run()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Task58] Shader '{ShaderName}' not found. Ensure BoltStrikerCrackle.shader compiled " +
                               "(check the Console for shader errors), then re-run.");
                return;
            }

            var material = EnsureMaterial(shader);
            int wired = AssignToPrefab(material);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Task58] Bolt Striker crackle setup complete. Material '{MaterialPath}' added to {wired} " +
                      "renderer slot(s). Tune _CrackleColor / _CrackleSpeed / _CrackleDensity / _ArcIntensity on the " +
                      "material to taste (arc speed/density is a by-eye call).");
        }

        private static Material EnsureMaterial(Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(MaterialFolder))
                    AssetDatabase.CreateFolder("Assets", "Materials");

                material = new Material(shader) { name = "BoltStrikerCrackle" };
                material.SetColor("_CrackleColor", new Color(1.0f, 0.8f, 0.2f, 1.0f)); // gold (Task 046 palette)
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            // Copy the shared base albedo + cutoff so the crackle respects the same alpha cut-outs. Bolt Striker
            // uses two base materials, but both draw from the same PolygonFantasyCharacters atlas, so this covers
            // both; thin arcs make any minor mismatch negligible anyway.
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
                Debug.LogWarning($"[Task58] Base material not found at '{BaseMaterialPath}'; the crackle won't match " +
                                 "alpha cut-outs (harmless). Assign _BaseMap on the material if needed.");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static int AssignToPrefab(Material overlay)
        {
            var root = PrefabUtility.LoadPrefabContents(BoltStrikerPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[Task58] Bolt Striker prefab not found at '{BoltStrikerPrefabPath}'. Material " +
                               "created but not assigned — drag it on as a 2nd material slot manually.");
                return 0;
            }

            int wired = 0;
            try
            {
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;

                    var mats = new List<Material>(r.sharedMaterials);
                    if (mats.Contains(overlay)) continue; // idempotent

                    // Only a clean EXTRA pass when materials already cover every submesh (Synty parts are
                    // single-submesh). Skip + warn on multi-submesh to avoid displacing a base material.
                    var mesh = GetSharedMesh(r);
                    if (mesh != null && mesh.subMeshCount > mats.Count)
                    {
                        Debug.LogWarning($"[Task58] '{r.name}' has {mesh.subMeshCount} submeshes but {mats.Count} " +
                                         "material(s); skipped to avoid displacing a base material. Add the overlay " +
                                         "material manually as an extra slot on this renderer if it needs the crackle.");
                        continue;
                    }

                    mats.Add(overlay);
                    r.sharedMaterials = mats.ToArray();
                    wired++;
                }

                if (wired > 0) PrefabUtility.SaveAsPrefabAsset(root, BoltStrikerPrefabPath);
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
