using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace KusakaFactory.Declavatar.EditorExtension
{
    public class SettingWindow : EditorWindow
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

    internal sealed class Configuration
    {
        private const string EDITOR_USER_SETTINGS_KEY = "DeclavatarConfiguration";

        internal string[] LibraryRelativePath;

        internal Configuration()
        {
            LibraryRelativePath = new string[]
            {
                    "Packages/org.kb10uy.declavatar/Extra",
            };
        }

        internal void SaveEditorUserSettings()
        {
            var configJson = JsonConvert.SerializeObject(this);
            EditorUserSettings.SetConfigValue(EDITOR_USER_SETTINGS_KEY, configJson);
        }

        internal static Configuration LoadEditorUserSettings()
        {
            var configJson = EditorUserSettings.GetConfigValue(EDITOR_USER_SETTINGS_KEY);
            return configJson != null ? JsonConvert.DeserializeObject<Configuration>(configJson) : new Configuration();
        }
    }
}
