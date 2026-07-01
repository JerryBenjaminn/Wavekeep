#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Wavekeep.Gear;
using Wavekeep.Progression;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 77 — developer tool: wipe ALL persistent cross-run progression back to a true first-launch state, so the
    /// new-player experience can be tested without hand-deleting save files. Testing convenience only — NOT a
    /// player-facing feature, no gameplay logic, no new systems.
    ///
    /// Every save the project writes lives as a JSON file under <see cref="Application.persistentDataPath"/>, named
    /// by each manager's own <c>DefaultSaveFileName</c> constant (reused here so the paths can never drift from what
    /// the game actually writes). The project uses NO <c>PlayerPrefs</c> for save data (verified), so nothing is
    /// cleared there — a blanket <c>PlayerPrefs.DeleteAll</c> would risk unrelated editor keys and is deliberately
    /// avoided.
    ///
    /// Persistent sources wiped (all in <c>persistentDataPath</c>):
    /// <list type="bullet">
    /// <item><c>gear_save.json</c> — owned gear instances, per-hero loadouts, Salvage Dust (Task 67 v2 format).</item>
    /// <item><c>hero_slot_unlocks.json</c> — persistent hero-slot unlock ceiling / wave milestones (Tasks 42/61–64).</item>
    /// <item><c>talent_discovery.json</c> — discovered apexes/combos codex (Task 43).</item>
    /// </list>
    /// Pure file IO — runs in Edit OR Play mode and never needs the game running. NOTE: if run DURING Play mode, an
    /// already-loaded <c>GameSession</c> still holds its state in memory and may re-save on the next change; stop and
    /// restart Play (or relaunch) for the wipe to take full effect. No gameplay code, save format, or SO is touched.
    /// </summary>
    public static class Task77ProgressionReset
    {
        // (label, file name) for every persistent save. File names come from the runtime constants, not literals,
        // so renaming a save file in its manager automatically keeps this tool correct.
        private static readonly (string label, string fileName)[] SaveFiles =
        {
            ("Gear save (instances, loadouts, Dust)", GearManager.DefaultSaveFileName),
            ("Hero slot unlocks", HeroSlotUnlockManager.DefaultSaveFileName),
            ("Talent discovery codex", TalentDiscoveryManager.DefaultSaveFileName),
        };

        [MenuItem("Wavekeep/Debug/Reset All Progression")]
        public static void ResetAllProgression()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Reset ALL Progression?",
                "This permanently deletes every persistent save under:\n" + Application.persistentDataPath +
                "\n\n• Gear (instances, loadouts, Salvage Dust)\n• Hero slot unlocks\n• Talent discovery codex\n\n" +
                "The next launch starts as a brand-new player. This cannot be undone.",
                "Wipe everything", "Cancel");
            if (!confirmed) return;

            var deleted = new List<string>();
            var absent = new List<string>();
            var failed = new List<string>();

            foreach (var (label, fileName) in SaveFiles)
            {
                if (string.IsNullOrEmpty(fileName)) continue;
                string path = Path.Combine(Application.persistentDataPath, fileName);
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        deleted.Add($"{label} ({fileName})");
                    }
                    else
                    {
                        absent.Add($"{label} ({fileName})");
                    }
                }
                catch (System.Exception e)
                {
                    failed.Add($"{label} ({fileName}): {e.Message}");
                }
            }

            LogResult(deleted, absent, failed);
        }

        private static void LogResult(List<string> deleted, List<string> absent, List<string> failed)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Wavekeep: full progression reset complete — fresh first-launch state.");
            sb.AppendLine($"Location: {Application.persistentDataPath}");

            sb.AppendLine(deleted.Count > 0
                ? $"Deleted ({deleted.Count}): " + string.Join(", ", deleted)
                : "Deleted (0): nothing to delete.");

            if (absent.Count > 0)
                sb.AppendLine($"Already absent ({absent.Count}): " + string.Join(", ", absent));

            if (Application.isPlaying)
                sb.AppendLine("NOTE: run during Play mode — a live GameSession may still hold cached state and " +
                              "re-save it. Stop and restart Play mode (or relaunch) for the wipe to take full effect.");

            if (failed.Count > 0)
            {
                sb.AppendLine($"FAILED ({failed.Count}): " + string.Join(" | ", failed));
                Debug.LogError("[Task77] " + sb.ToString());
            }
            else
            {
                Debug.Log("[Task77] " + sb.ToString());
            }
        }
    }
}
#endif
