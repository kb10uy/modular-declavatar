using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using KusakaFactory.Declavatar.Runtime;

namespace KusakaFactory.Declavatar.EditorExtension
{
    [CustomEditor(typeof(DeclavatarExternalAssets))]
    internal sealed class ExternalAssetsInspector : Editor
    {
        private SerializedProperty _entriesProperty;
        private ReorderableList _entriesList;

        private readonly static Dictionary<string, Type> AssetTypes = new Dictionary<string, Type>
        {
            [nameof(Material)] = typeof(Material),
            [nameof(AnimationClip)] = typeof(AnimationClip),
            [nameof(AudioClip)] = typeof(AudioClip),
            [nameof(AvatarMask)] = typeof(AvatarMask),
            [nameof(GameObject)] = typeof(GameObject),
        };
        private readonly static string[] AssetTypeStrings = AssetTypes.Keys.ToArray();

        public void OnEnable()
        {
            _entriesProperty = serializedObject.FindProperty(nameof(DeclavatarExternalAssets.Entries));
            _entriesList = new ReorderableList(serializedObject, _entriesProperty)
            {
                onAddCallback = AddItem,
                drawHeaderCallback = DrawHeader,
                elementHeightCallback = GetElementHeight,
                drawElementCallback = DrawElement,
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _entriesList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        private void AddItem(ReorderableList list)
        {
            var lastIndex = _entriesProperty.arraySize;
            Debug.Log($"Adding {lastIndex}");

            _entriesProperty.InsertArrayElementAtIndex(lastIndex);
            var newItemProperty = _entriesProperty.GetArrayElementAtIndex(lastIndex);
            newItemProperty.FindPropertyRelative("Key").stringValue = "";
            newItemProperty.FindPropertyRelative("Type").stringValue = "Material";
            newItemProperty.FindPropertyRelative("Asset").objectReferenceValue = null;
        }

        private void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "External Asset Definitions");
        }

        private float GetElementHeight(int index)
        {
            return EditorGUIUtility.singleLineHeight * 2 + 8;
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool focused)
        {
            var itemProperty = _entriesProperty.GetArrayElementAtIndex(index);
            var itemKey = itemProperty.FindPropertyRelative("Key");
            var itemType = itemProperty.FindPropertyRelative("Type");
            var itemAsset = itemProperty.FindPropertyRelative("Asset");

            var previousKey = itemKey.stringValue;
            var previousTypeString = itemType.stringValue;
            var previousTypeIndex = Array.IndexOf(AssetTypeStrings, previousTypeString);
            var previousType = AssetTypes.GetValueOrDefault(previousTypeString);
            if (previousTypeIndex == -1)
            {
                previousTypeIndex = 0;
                previousType = typeof(Material);
            }

            // Prefix Label
            var prefixRect = new Rect(rect.x, rect.y, 120, EditorGUIUtility.singleLineHeight);
            var labelText = string.IsNullOrEmpty(previousKey) ? $"Element {index}" : previousKey;
            EditorGUI.PrefixLabel(prefixRect, new GUIContent(labelText));
            rect.x += 120;
            rect.width -= 120;

            // Key
            var keyRect = new Rect(rect.x, rect.y + 2, rect.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(keyRect, itemKey, GUIContent.none);

            // Type and Asset
            var typeRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 6, 120, EditorGUIUtility.singleLineHeight);
            var newTypeIndex = EditorGUI.Popup(typeRect, previousTypeIndex, AssetTypeStrings);
            itemType.stringValue = AssetTypeStrings[newTypeIndex];

            if (previousTypeIndex != newTypeIndex) itemAsset.objectReferenceValue = null;
            var valueRect = new Rect(rect.x + 120, rect.y + EditorGUIUtility.singleLineHeight + 6, rect.width - 120, EditorGUIUtility.singleLineHeight);
            EditorGUI.ObjectField(valueRect, itemAsset, previousType, GUIContent.none);
        }
    }
}
