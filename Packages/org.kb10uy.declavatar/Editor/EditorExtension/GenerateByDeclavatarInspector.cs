using UnityEngine;
using UnityEditor;
using KusakaFactory.Declavatar.Runtime;

namespace KusakaFactory.Declavatar.EditorExtension
{
    [CustomEditor(typeof(GenerateByDeclavatar))]
    internal sealed class GenerateByDeclavatarInspector : Editor
    {
        private SerializedProperty _formatProperty;
        private SerializedProperty _definitionProperty;
        private SerializedProperty _externalAssetsProperty;
        private SerializedProperty _symbolsProperty;
        private SerializedProperty _declarationRootProperty;
        private SerializedProperty _installTargetProperty;
        private SerializedProperty _generateMenuInstallerProperty;

        public void OnEnable()
        {
            _formatProperty = serializedObject.FindProperty("Format");
            _definitionProperty = serializedObject.FindProperty("Definition");
            _externalAssetsProperty = serializedObject.FindProperty("ExternalAssets");
            _symbolsProperty = serializedObject.FindProperty("Symbols");
            _declarationRootProperty = serializedObject.FindProperty("DeclarationRoot");
            _installTargetProperty = serializedObject.FindProperty("InstallTarget");
            _generateMenuInstallerProperty = serializedObject.FindProperty("GenerateMenuInstaller");
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

            EditorGUILayout.PropertyField(_externalAssetsProperty);
            EditorGUILayout.PropertyField(_symbolsProperty);
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(_declarationRootProperty);
            EditorGUILayout.PropertyField(_installTargetProperty);
            EditorGUILayout.PropertyField(_generateMenuInstallerProperty);

            serializedObject.ApplyModifiedProperties();
        }

        internal static class Constants
        {
            internal static GUIStyle TitleLabel { get; private set; }

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
