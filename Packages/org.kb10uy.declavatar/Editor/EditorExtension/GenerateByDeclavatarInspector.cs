using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using KusakaFactory.Declavatar.Runtime;

namespace KusakaFactory.Declavatar.EditorExtension
{
    [CustomEditor(typeof(GenerateByDeclavatar))]
    internal sealed class GenerateByDeclavatarInspector : Editor
    {
        private SerializedProperty _formatProperty;
        private SerializedProperty _definitionProperty;
        private SerializedProperty _externalAssetsProperty;
        private SerializedProperty _declarationRootProperty;
        private SerializedProperty _installTargetProperty;
        private SerializedProperty _generateMenuInstallerProperty;

        private ReorderableList _externalAssetsList;

        public void OnEnable()
        {
            _formatProperty = serializedObject.FindProperty("Format");
            _definitionProperty = serializedObject.FindProperty("Definition");
            _externalAssetsProperty = serializedObject.FindProperty("ExternalAssets");
            _declarationRootProperty = serializedObject.FindProperty("DeclarationRoot");
            _installTargetProperty = serializedObject.FindProperty("InstallTarget");
            _generateMenuInstallerProperty = serializedObject.FindProperty("GenerateMenuInstaller");

            _externalAssetsList = new ReorderableList(serializedObject, _externalAssetsProperty)
            {
                drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "External Assets"),
                elementHeightCallback = (index) => EditorGUIUtility.singleLineHeight,
                drawElementCallback = (rect, index, isActive, focused) =>
                {
                    var itemProperty = _externalAssetsProperty.GetArrayElementAtIndex(index);
                    rect.height = EditorGUIUtility.singleLineHeight;
                    rect = EditorGUI.PrefixLabel(rect, new GUIContent($"Element {index}"));
                    EditorGUI.PropertyField(rect, itemProperty, GUIContent.none);
                },
            };
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Declavatar", Constants.TitleLabel);
            GUILayout.Label("Declarative Avatar Assets Composing Tool, by kb10uy", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Separator();
            EditorGUILayout.EndVertical();

            serializedObject.Update();
            EditorGUILayout.PropertyField(_formatProperty);
            EditorGUILayout.PropertyField(_definitionProperty);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_declarationRootProperty);
            EditorGUILayout.PropertyField(_installTargetProperty);
            EditorGUILayout.PropertyField(_generateMenuInstallerProperty);
            _externalAssetsList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        internal static class Constants
        {
            public static GUIStyle TitleLabel { get; private set; }

            static Constants()
            {
                TitleLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter,
                    margin = new RectOffset(8, 8, 4, 4),
                };
            }
        }
    }
}
