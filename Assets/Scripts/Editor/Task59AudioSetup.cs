#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Wavekeep.Audio;
using Wavekeep.Core;

namespace Wavekeep.EditorTools
{
    /// <summary>
    /// Task 59 — one-time, idempotent setup for the audio system's assets. Creates the Assets/Audio folder tree,
    /// the WavekeepAudioMixer (Master → Music/SFX/UI), clip-less placeholder cue SOs for each wired EventBus
    /// event, the WavekeepAudioConfig (wiring those cues + the mixer groups), and assigns the config to the
    /// scene's GameSessionBootstrap. No audio clips are assigned — the developer fills those in afterward.
    ///
    /// The AudioMixer is created via reflection on UnityEditor's internal AudioMixerController (there is no public
    /// API to create a mixer + groups). If that ever fails on a Unity version, the script logs exact manual steps
    /// and everything else still works (AudioManager simply routes to the default output until the groups exist).
    /// </summary>
    public static class Task59AudioSetup
    {
        private const string AudioRoot = "Assets/Audio";
        private const string MixerPath = "Assets/Audio/WavekeepAudioMixer.mixer";
        private const string ConfigPath = "Assets/Audio/WavekeepAudioConfig.asset";

        [MenuItem("Wavekeep/Setup Task 59 (Audio System)")]
        public static void Run()
        {
            CreateFolders();

            var mixer = EnsureMixer();

            // Placeholder cues (clip-less). Folder = organisation; category = mixer routing.
            var enemyDeath   = EnsureCue("Assets/Audio/SFX/Enemies/Cue_EnemyDeath.asset",     AudioCategory.SFX);
            var waveStart    = EnsureCue("Assets/Audio/SFX/Environment/Cue_WaveStart.asset",  AudioCategory.SFX);
            var waveComplete = EnsureCue("Assets/Audio/SFX/Environment/Cue_WaveComplete.asset",AudioCategory.SFX);
            var levelUp      = EnsureCue("Assets/Audio/SFX/UI/Cue_LevelUp.asset",             AudioCategory.UI);
            var victory      = EnsureCue("Assets/Audio/Music/Cue_Victory.asset",              AudioCategory.Music);
            var defeat       = EnsureCue("Assets/Audio/Music/Cue_Defeat.asset",               AudioCategory.Music);
            // Looping scene beds (started on scene/run begin by the bootstrap).
            var bgMusic      = EnsureCue("Assets/Audio/Music/Cue_GameplayMusic.asset",        AudioCategory.Music, loop: true);
            var ambient      = EnsureCue("Assets/Audio/SFX/Environment/Cue_Ambience.asset",   AudioCategory.SFX,   loop: true);

            var config = EnsureConfig();
            WireConfig(config, mixer, enemyDeath, waveStart, waveComplete, levelUp, victory, defeat, bgMusic, ambient);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WireBootstrap(config);

            Debug.Log("[Task59] Audio system assets created (folders, mixer, 6 placeholder cues, config) and the " +
                      "config assigned to GameSessionBootstrap. Drop AudioClips onto the Cue_* assets in " +
                      "Assets/Audio/. Save the scene (Ctrl+S).");
        }

        // ---- Folders ----------------------------------------------------------------------------------------

        private static void CreateFolders()
        {
            EnsureFolder("Assets/Audio");
            EnsureFolder("Assets/Audio/SFX");
            EnsureFolder("Assets/Audio/SFX/Heroes");
            EnsureFolder("Assets/Audio/SFX/Enemies");
            EnsureFolder("Assets/Audio/SFX/UI");
            EnsureFolder("Assets/Audio/SFX/Environment");
            EnsureFolder("Assets/Audio/Music");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ---- Mixer (reflection over the internal AudioMixerController) --------------------------------------

        private static AudioMixer EnsureMixer()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null) return existing;

            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static;
            var ctrlType = FindType("UnityEditor.Audio.AudioMixerController");
            var create = ctrlType?.GetMethod("CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (create == null)
            {
                Debug.LogWarning("[Task59] Could not create the AudioMixer programmatically (internal API not " +
                                 "found). MANUAL STEP: Assets ▸ Create ▸ Audio Mixer named 'WavekeepAudioMixer' in " +
                                 "Assets/Audio/, add child groups 'Music', 'SFX', 'UI' under Master, then re-run " +
                                 "this menu to wire them into the config.");
                return null;
            }

            try
            {
                var ctrl = create.Invoke(null, new object[] { MixerPath }) as UnityEngine.Object;
                if (ctrl == null) return null;

                var master = ctrlType.GetProperty("masterGroup", Any)?.GetValue(ctrl);
                var createGroup = ctrlType.GetMethod("CreateNewGroup", Any, null,
                    new[] { typeof(string), typeof(bool) }, null);
                var addChild = ctrlType.GetMethod("AddChildToParent", Any);
                var addToView = ctrlType.GetMethod("AddGroupToCurrentView", Any);

                if (master != null && createGroup != null && addChild != null)
                {
                    foreach (var groupName in new[] { "Music", "SFX", "UI" })
                    {
                        var group = createGroup.Invoke(ctrl, new object[] { groupName, false });
                        addChild.Invoke(ctrl, new[] { group, master });
                        addToView?.Invoke(ctrl, new[] { group });
                    }
                }
                else
                {
                    Debug.LogWarning("[Task59] Mixer created but couldn't add groups via reflection — add 'Music', " +
                                     "'SFX', 'UI' under Master manually, then re-run to wire them.");
                }

                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Task59] AudioMixer group setup threw ({e.GetType().Name}: {e.Message}). The " +
                                 "mixer asset may need its Music/SFX/UI groups added manually, then re-run.");
            }

            return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            if (mixer == null) return null;
            var groups = mixer.FindMatchingGroups(name);
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }

        // ---- Cue + config assets ---------------------------------------------------------------------------

        private static AudioCueDefinitionSO EnsureCue(string path, AudioCategory category, bool loop = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioCueDefinitionSO>(path);
            if (existing != null) return existing;

            var cue = ScriptableObject.CreateInstance<AudioCueDefinitionSO>();
            var so = new SerializedObject(cue);
            so.FindProperty("_category").enumValueIndex = (int)category;
            so.FindProperty("_loop").boolValue = loop;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(cue, path);
            return cue;
        }

        private static AudioConfigSO EnsureConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioConfigSO>(ConfigPath);
            if (existing != null) return existing;

            var config = ScriptableObject.CreateInstance<AudioConfigSO>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static void WireConfig(AudioConfigSO config, AudioMixer mixer,
            AudioCueDefinitionSO enemyDeath, AudioCueDefinitionSO waveStart, AudioCueDefinitionSO waveComplete,
            AudioCueDefinitionSO levelUp, AudioCueDefinitionSO victory, AudioCueDefinitionSO defeat,
            AudioCueDefinitionSO bgMusic, AudioCueDefinitionSO ambient)
        {
            var so = new SerializedObject(config);
            so.FindProperty("_backgroundMusicCue").objectReferenceValue = bgMusic;
            so.FindProperty("_ambientCue").objectReferenceValue = ambient;
            so.FindProperty("_enemyKilledCue").objectReferenceValue = enemyDeath;
            so.FindProperty("_waveStartedCue").objectReferenceValue = waveStart;
            so.FindProperty("_waveCompletedCue").objectReferenceValue = waveComplete;
            so.FindProperty("_levelUpCue").objectReferenceValue = levelUp;
            so.FindProperty("_victoryCue").objectReferenceValue = victory;
            so.FindProperty("_defeatCue").objectReferenceValue = defeat;

            so.FindProperty("_musicGroup").objectReferenceValue = FindGroup(mixer, "Music");
            so.FindProperty("_sfxGroup").objectReferenceValue = FindGroup(mixer, "SFX");
            so.FindProperty("_uiGroup").objectReferenceValue = FindGroup(mixer, "UI");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // ---- Scene wiring ----------------------------------------------------------------------------------

        private static void WireBootstrap(AudioConfigSO config)
        {
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<GameSessionBootstrap>(FindObjectsInactive.Include);
            if (bootstrap == null)
            {
                Debug.LogWarning("[Task59] No GameSessionBootstrap in the open scene — assign WavekeepAudioConfig " +
                                 "to its 'Audio Config' field manually in the gameplay scene.");
                return;
            }

            var so = new SerializedObject(bootstrap);
            var prop = so.FindProperty("_audioConfig");
            if (prop == null)
            {
                Debug.LogError("[Task59] GameSessionBootstrap has no '_audioConfig' field — script/asset out of sync.");
                return;
            }
            prop.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
#endif
