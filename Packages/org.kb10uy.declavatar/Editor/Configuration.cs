using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace KusakaFactory.Declavatar
{
    internal sealed class Configuration
    {
        private const string EDITOR_USER_SETTINGS_KEY = "DeclavatarConfiguration";

        internal string[] LibraryRelativePath;

        public Configuration()
        {
            LibraryRelativePath = new string[]
            {
                "Packages/org.kb10uy.declavatar/Extra",
            };
        }

        public void SaveEditorUserSettings()
        {
            var configJson = JsonConvert.SerializeObject(this);
            EditorUserSettings.SetConfigValue(EDITOR_USER_SETTINGS_KEY, configJson);
        }

        public static Configuration LoadEditorUserSettings()
        {
            var configJson = EditorUserSettings.GetConfigValue(EDITOR_USER_SETTINGS_KEY);
            return configJson != null ? JsonConvert.DeserializeObject<Configuration>(configJson) : new Configuration();
        }

        public IEnumerable<string> EnumerateAbsoluteLibraryPaths()
        {
            var assetsPath = Application.dataPath;
            var projectPath = Path.GetDirectoryName(assetsPath);

            return LibraryRelativePath
                .Select((p) => p.Trim())
                .Where((p) => !string.IsNullOrEmpty(p))
                .Select((p) => Path.Combine(projectPath, p));
        }
    }
}
