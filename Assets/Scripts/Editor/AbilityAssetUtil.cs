#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Wavekeep.Data;

namespace Wavekeep.EditorTools
{
    /// <summary>Shared editor helpers for authoring ability/upgrade SO assets (Task 04/05 setups).</summary>
    public static class AbilityAssetUtil
    {
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        /// <summary>Populate a serialized <see cref="TagInteractionRule"/> list element.</summary>
        public static void SetRule(SerializedProperty element, UpgradeTag tag, AbilityModifierType type, float value)
        {
            element.FindPropertyRelative("_matchTag").enumValueIndex = (int)tag;
            element.FindPropertyRelative("_modifierType").enumValueIndex = (int)type;
            element.FindPropertyRelative("_modifierValue").floatValue = value;
        }
    }
}
#endif
