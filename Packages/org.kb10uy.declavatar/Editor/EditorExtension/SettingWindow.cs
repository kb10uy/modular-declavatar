using UnityEngine;
using UnityEditor;

namespace KusakaFactory.Declavatar.EditorExtension
{
    internal sealed  class SettingWindow : EditorWindow
    {
        [SerializeField]
        private string[] _libraryPaths;

        private Vector2 _scrollPosition = Vector2.zero;

        [MenuItem("Tools/Declavatar/Settings")]
        internal static SettingWindow ShowLogWindow()
        {
            var window = GetWindow<SettingWindow>("Declavatar Settings", true);
            var position = window.position;
            position.size = new Vector2(400.0f, 100.0f);
            window.position = position;

            window.LoadConfigValue();
            return window;
        }

        public void OnGUI()
        {
            var serializedObject = new SerializedObject(this);
            serializedObject.Update();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_libraryPaths"));
            if (GUILayout.Button("Save")) SaveConfigValue();

            EditorGUILayout.EndScrollView();
            serializedObject.ApplyModifiedProperties();
        }

        private void SaveConfigValue()
        {
            var config = new Configuration
            {
                LibraryRelativePath = _libraryPaths,
            };
            config.SaveEditorUserSettings();
        }

        private void LoadConfigValue()
        {
            var config = Configuration.LoadEditorUserSettings();
            _libraryPaths = config.LibraryRelativePath;
        }
    }
}
